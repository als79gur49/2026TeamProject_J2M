using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxAuthoringBindingTests
    {
        private const string JumperLandingTargetBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperLandingTarget_Binding.asset";
        private const string UtilityWindupBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyUtilityWindupTelegraph_Binding.asset";
        private const string ForwardCellDangerMarkerBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ForwardCellDangerMarker_Binding.asset";
        private const string ForwardCellProjectileActiveBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ForwardCellProjectileActive_Binding.asset";
        private const string ForwardCellProjectileFlightFollowBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ForwardCellProjectileFlightFollow_Binding.asset";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private static readonly string[] SourceCloneMotionBindingPaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxDestroyShrink_Binding.asset",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipDestroySelfMotion_Binding.asset",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ImpactTransientBreak_Binding.asset",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxOutOfBoundsExit_Binding.asset",
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyOutOfBoundsExit_Binding.asset",
        };

        [Test]
        [Category("Extended")]
        public void JumperLandingTarget_BindingAsset_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(JumperLandingTargetBindingPath);

            Assert.That(binding, Is.Not.Null, JumperLandingTargetBindingPath);
            var validation = binding.ValidateAuthoring();
            var policy = binding.BuildRuntimePolicy();

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(binding.Prefab, Is.Not.Null);
            Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Loop));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.StopEmittingThenRelease));
            Assert.That(policy.DefaultLifetimeSeconds, Is.Zero);
            Assert.That(policy.TailSeconds, Is.Zero);
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(8));
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_ContainsJumperLandingTarget()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            var validation = cueMap.ValidateAuthoring();
            var runtimeMap = cueMap.BuildRuntimeMap();

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(
                runtimeMap.TryResolve(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget), out var policy),
                Is.True);
            Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget), out var prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void UtilityWindupTelegraph_BindingAsset_RemovedFromDefaultAuthoring()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(UtilityWindupBindingPath);

            Assert.That(binding, Is.Null, UtilityWindupBindingPath);
        }

        [Test]
        [Category("Extended")]
        public void ForwardCellOptionalProjectileBindings_RemovedFromDefaultAuthoring()
        {
            var dangerMarkerBinding =
                AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ForwardCellDangerMarkerBindingPath);
            var activeBinding =
                AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ForwardCellProjectileActiveBindingPath);
            var flightFollowBinding =
                AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ForwardCellProjectileFlightFollowBindingPath);
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(dangerMarkerBinding, Is.Null, ForwardCellDangerMarkerBindingPath);
            Assert.That(activeBinding, Is.Null, ForwardCellProjectileActiveBindingPath);
            Assert.That(flightFollowBinding, Is.Null, ForwardCellProjectileFlightFollowBindingPath);
            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(
                GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellDangerMarker),
                out _), Is.False);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(
                GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileActive),
                out _), Is.False);
            Assert.That(cueMap.BuildRuntimeMap().TryResolve(
                GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlightFollow),
                out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BindingAsset_BuildsRuntimePolicyWithoutPrefabLeak()
        {
            var prefab = CreateValidPrefab("ValidVfxPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefab,
                VfxBindingRequirement.Required,
                VfxMissingAnchorPolicy.FailFast,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 1.25f,
                tailSeconds: 0.5f,
                initialPoolSize: 2,
                maxConcurrentInstances: 3);

            try
            {
                var policy = binding.BuildRuntimePolicy();

                Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(PlayerVfxCue.Damage)));
                Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.Required));
                Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.FailFast));
                Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
                Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabOnly));
                Assert.That(policy.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.ExplicitPrefabRequired));
                Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
                Assert.That(policy.DefaultLifetimeSeconds, Is.EqualTo(1.25f));
                Assert.That(policy.TailSeconds, Is.EqualTo(0.5f));
                Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(3));
                Assert.That(
                    typeof(VfxBindingRuntimePolicy)
                        .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(field => field.FieldType == typeof(GameObject)),
                    Is.False);
            }
            finally
            {
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void BindingAsset_RejectsNullPrefab_ForPrefabOnly()
        {
            var required = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefab: null,
                requirement: VfxBindingRequirement.Required,
                missingAnchorPolicy: VfxMissingAnchorPolicy.FailFast);
            var optional = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.RecoveryDust,
                prefab: null,
                requirement: VfxBindingRequirement.Optional,
                missingAnchorPolicy: VfxMissingAnchorPolicy.SkipOptional);

            try
            {
                Assert.That(required.ValidateAuthoring().HasErrors, Is.True);
                Assert.That(optional.ValidateAuthoring().HasErrors, Is.True);
                Assert.Throws<InvalidOperationException>(() => required.BuildRuntimePolicy());
                Assert.Throws<InvalidOperationException>(() => optional.BuildRuntimePolicy());
            }
            finally
            {
                Destroy(required);
                Destroy(optional);
            }
        }

        [Test]
        [Category("Core")]
        public void BindingAsset_AllowsNullPrefab_ForSourceCloneMotion_WhenCommonHostAllowed()
        {
            var binding = CreateBinding(
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.DestroyShrink,
                prefab: null,
                requirement: VfxBindingRequirement.DiagnosticIfMissing,
                missingAnchorPolicy: VfxMissingAnchorPolicy.ReportDiagnostic,
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed,
                defaultLifetimeSeconds: 0f,
                tailSeconds: 0.18f,
                initialPoolSize: 4,
                maxConcurrentInstances: 12);

            try
            {
                var validation = binding.ValidateAuthoring();
                var policy = binding.BuildRuntimePolicy();

                Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
                Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
                Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.DestroyShrink)));
                Assert.That(policy.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion));
                Assert.That(policy.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed));
            }
            finally
            {
                Destroy(binding);
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotionBindings_DoNotRequireCueSpecificPrefab()
        {
            foreach (var path in SourceCloneMotionBindingPaths)
            {
                var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(path);

                Assert.That(binding, Is.Not.Null, path);
                Assert.That(binding.Prefab, Is.Null, path);
                Assert.That(binding.VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.SourceCloneMotion), path);
                Assert.That(binding.HostRequirement, Is.EqualTo(GameplayVfxHostRequirement.CommonHostAllowed), path);
                Assert.That(binding.ValidateAuthoring().HasErrors, Is.False, path);
                Assert.That(binding.ValidateAuthoring().HasWarnings, Is.False, path);
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotionCueSpecificPlaceholder_IsGovernanceWarning_IfReferenced()
        {
            var prefab = CreateValidPrefab("SourceCloneHostOnlyPlaceholderPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.DestroyShrink,
                prefab: prefab,
                requirement: VfxBindingRequirement.DiagnosticIfMissing,
                missingAnchorPolicy: VfxMissingAnchorPolicy.ReportDiagnostic,
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);

            try
            {
                var validation = binding.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_BINDING_SOURCE_CLONE_CUE_SPECIFIC_PLACEHOLDER_PREFAB"));
                Assert.DoesNotThrow(() => binding.BuildRuntimePolicy());
            }
            finally
            {
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotionCueReferencesActualVisualPrefab_IsGovernanceWarning()
        {
            var prefab = CreateVisualPrefab("SourceCloneActualVisualPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Box,
                (int)BoxVfxCue.DestroyShrink,
                prefab: prefab,
                requirement: VfxBindingRequirement.DiagnosticIfMissing,
                missingAnchorPolicy: VfxMissingAnchorPolicy.ReportDiagnostic,
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);

            try
            {
                var validation = binding.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_BINDING_SOURCE_CLONE_ACTUAL_VISUAL_PREFAB"));
                Assert.DoesNotThrow(() => binding.BuildRuntimePolicy());
            }
            finally
            {
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Core")]
        public void BindingAsset_ValidatesFallbackPrefab_ForPrefabWithSourceClone_WhenFallbackRequired()
        {
            var prefab = CreateValidPrefab("PrefabWithSourceCloneFallbackPrefab");
            var valid = CreateBinding(
                GameplayVfxFamily.Enemy,
                (int)EnemyVfxCue.DeathMotion,
                prefab: prefab,
                requirement: VfxBindingRequirement.DiagnosticIfMissing,
                missingAnchorPolicy: VfxMissingAnchorPolicy.ReportDiagnostic,
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone,
                hostRequirement: GameplayVfxHostRequirement.ExplicitPrefabRequired,
                defaultLifetimeSeconds: 0f,
                tailSeconds: 0.2f,
                initialPoolSize: 4,
                maxConcurrentInstances: 8);
            var missingFallback = CreateBinding(
                GameplayVfxFamily.Enemy,
                (int)EnemyVfxCue.DeathMotion,
                prefab: null,
                requirement: VfxBindingRequirement.DiagnosticIfMissing,
                missingAnchorPolicy: VfxMissingAnchorPolicy.ReportDiagnostic,
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone,
                hostRequirement: GameplayVfxHostRequirement.ExplicitPrefabRequired,
                defaultLifetimeSeconds: 0f,
                tailSeconds: 0.2f,
                initialPoolSize: 4,
                maxConcurrentInstances: 8);

            try
            {
                Assert.That(valid.ValidateAuthoring().HasErrors, Is.False);
                Assert.That(valid.BuildRuntimePolicy().VisualSourceMode, Is.EqualTo(VfxVisualSourceMode.PrefabWithSourceClone));
                Assert.That(missingFallback.ValidateAuthoring().HasErrors, Is.True);
                Assert.Throws<InvalidOperationException>(() => missingFallback.BuildRuntimePolicy());
            }
            finally
            {
                Destroy(missingFallback);
                Destroy(valid);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void BindingAsset_RejectsInvalidPolicyValues()
        {
            var prefabA = CreateValidPrefab("InvalidDurationVfxPrefab");
            var prefabB = CreateValidPrefab("RequiredSkipOptionalVfxPrefab");
            var invalidDuration = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefabA,
                defaultLifetimeSeconds: -0.1f);
            var requiredSkipOptional = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.RecoveryDust,
                prefabB,
                VfxBindingRequirement.Required,
                VfxMissingAnchorPolicy.SkipOptional);

            try
            {
                Assert.That(invalidDuration.ValidateAuthoring().HasErrors, Is.True);
                Assert.That(
                    invalidDuration.ValidateAuthoring().Messages.Select(message => message.Code),
                    Does.Contain("VFX_BINDING_POLICY_INVALID"));
                Assert.That(requiredSkipOptional.ValidateAuthoring().HasErrors, Is.True);
                Assert.That(
                    requiredSkipOptional.ValidateAuthoring().Messages.Select(message => message.Code),
                    Does.Contain("VFX_BINDING_REQUIRED_SKIP_OPTIONAL"));
            }
            finally
            {
                Destroy(invalidDuration);
                Destroy(requiredSkipOptional);
                Destroy(prefabA);
                Destroy(prefabB);
            }
        }

        [Test]
        [Category("Extended")]
        public void BindingAsset_RejectsPrefabWithoutModelRootContract()
        {
            var prefab = new GameObject("MissingModelRootBindingPrefab");
            prefab.AddComponent<ParticleSystem>();
            var binding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.Damage,
                prefab);

            try
            {
                var validation = binding.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_MODEL_ROOT_MISSING"));
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PREFAB_ROOT_COMPONENT"));
            }
            finally
            {
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void BindingAsset_BuildsDiagnosticRequirementPolicy()
        {
            var prefab = CreateValidPrefab("DiagnosticVfxPrefab");
            var binding = CreateBinding(
                GameplayVfxFamily.Player,
                (int)PlayerVfxCue.PushWindup,
                prefab,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic);

            try
            {
                var policy = binding.BuildRuntimePolicy();

                Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
                Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            }
            finally
            {
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void CueMapAsset_RejectsNullBinding()
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", new VfxBindingDefinitionAsset[] { null });

            try
            {
                var validation = cueMap.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_CUE_MAP_NULL_BINDING"));
            }
            finally
            {
                Destroy(cueMap);
            }
        }

        [Test]
        [Category("Extended")]
        public void CueMapAsset_RejectsDuplicateCue()
        {
            var prefabA = CreateValidPrefab("VfxPrefabA");
            var prefabB = CreateValidPrefab("VfxPrefabB");
            var bindingA = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefabA);
            var bindingB = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefabB);
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", new[] { bindingA, bindingB });

            try
            {
                var validation = cueMap.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_CUE_MAP_DUPLICATE_CUE"));
                Assert.Throws<InvalidOperationException>(() => cueMap.BuildRuntimeMap());
            }
            finally
            {
                Destroy(cueMap);
                Destroy(bindingA);
                Destroy(bindingB);
                Destroy(prefabA);
                Destroy(prefabB);
            }
        }

        [Test]
        [Category("Extended")]
        public void CueMapAsset_BuildsRuntimeCueMap()
        {
            var prefabA = CreateValidPrefab("VfxPrefabA");
            var prefabB = CreateValidPrefab("VfxPrefabB");
            var damage = CreateBinding(GameplayVfxFamily.Player, (int)PlayerVfxCue.Damage, prefabA);
            var death = CreateBinding(GameplayVfxFamily.Player, (int)PlayerVfxCue.Death, prefabB);
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", new[] { death, damage });

            try
            {
                var runtimeMap = cueMap.BuildRuntimeMap();

                Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(PlayerVfxCue.Damage), out var damagePolicy), Is.True);
                Assert.That(runtimeMap.TryResolve(GameplayVfxCueId.From(PlayerVfxCue.Death), out var deathPolicy), Is.True);
                Assert.That(damagePolicy.CueId, Is.EqualTo(GameplayVfxCueId.From(PlayerVfxCue.Damage)));
                Assert.That(deathPolicy.CueId, Is.EqualTo(GameplayVfxCueId.From(PlayerVfxCue.Death)));
            }
            finally
            {
                Destroy(cueMap);
                Destroy(damage);
                Destroy(death);
                Destroy(prefabA);
                Destroy(prefabB);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProfileAsset_RejectsCrossFamilyBinding()
        {
            var prefab = CreateValidPrefab("BoxVfxPrefab");
            var binding = CreateBinding(GameplayVfxFamily.Box, (int)BoxVfxCue.DestroySmoke, prefab);
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetField(profile, "family", GameplayVfxFamily.Player);
            SetField(profile, "bindings", new[] { binding });

            try
            {
                var validation = profile.ValidateAuthoring();

                Assert.That(validation.HasErrors, Is.True);
                Assert.That(validation.Messages.Select(message => message.Code), Does.Contain("VFX_PROFILE_CROSS_FAMILY_BINDING"));
                Assert.Throws<InvalidOperationException>(() => profile.BuildRuntimeProfile());
            }
            finally
            {
                Destroy(profile);
                Destroy(binding);
                Destroy(prefab);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProfileAsset_BuildsRuntimeProfile()
        {
            var prefab = CreateValidPrefab("EnemyVfxPrefab");
            var binding = CreateBinding(GameplayVfxFamily.Enemy, (int)EnemyVfxCue.Spawn, prefab);
            var profile = ScriptableObject.CreateInstance<VfxProfileAsset>();
            SetField(profile, "family", GameplayVfxFamily.Enemy);
            SetField(profile, "bindings", new[] { binding });

            try
            {
                var runtimeProfile = profile.BuildRuntimeProfile();

                Assert.That(runtimeProfile.Family, Is.EqualTo(GameplayVfxFamily.Enemy));
                Assert.That(runtimeProfile.TryResolve(GameplayVfxCueId.From(EnemyVfxCue.Spawn), out var policy), Is.True);
                Assert.That(policy.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Spawn)));
                Assert.That(runtimeProfile.TryResolve(GameplayVfxCueId.From(PlayerVfxCue.Damage), out _), Is.False);
            }
            finally
            {
                Destroy(profile);
                Destroy(binding);
                Destroy(prefab);
            }
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameplayVfxFamily family,
            int cueCode,
            GameObject prefab,
            VfxBindingRequirement requirement = VfxBindingRequirement.Optional,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot,
            VfxStopPolicy stopPolicy = VfxStopPolicy.AuthoredDuration,
            VfxVisualSourceMode visualSourceMode = VfxVisualSourceMode.PrefabOnly,
            GameplayVfxHostRequirement hostRequirement = GameplayVfxHostRequirement.ExplicitPrefabRequired,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int initialPoolSize = 0,
            int maxConcurrentInstances = 0)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", family);
            SetField(binding, "cueCode", cueCode);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", requirement);
            SetField(binding, "missingAnchorPolicy", missingAnchorPolicy);
            SetField(binding, "playbackMode", playbackMode);
            SetField(binding, "visualSourceMode", visualSourceMode);
            SetField(binding, "hostRequirement", hostRequirement);
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

        private static GameObject CreateValidPrefab(string name)
        {
            var prefab = new GameObject(name);
            new GameObject("ModelRoot").transform.SetParent(prefab.transform, worldPositionStays: false);
            return prefab;
        }

        private static GameObject CreateVisualPrefab(string name)
        {
            var prefab = CreateValidPrefab(name);
            var visual = new GameObject("VisualParticles");
            visual.transform.SetParent(prefab.transform.Find("ModelRoot"), worldPositionStays: false);
            visual.AddComponent<ParticleSystem>();
            return prefab;
        }

        private static void Destroy(UnityEngine.Object unityObject)
        {
            if (unityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(unityObject);
            }
        }
    }
}
