using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraShakeMixerFoundationArchitectureTests
    {
        private const string FeedbackDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Camera/Feedback";
        private const string PresenterPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs";
        private const string RigPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs";
        private const string ProductionProfilePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayCameraShakeProfile_CampaignV1.asset";
        private const string CampaignFlowPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs";
        private const string AstretonPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Astreton.prefab";
        private const string NonHeavyReferencePrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_RocketFace.prefab";

        [Test]
        [Category("Extended")]
        public void CameraShakeRequest_IsTypedPresentationOnlyAndHasNoCallerKeyOrDelay()
        {
            var requestType = typeof(CameraShakeImpulseRequest);
            var propertyNames = requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(requestType.Assembly, Is.EqualTo(typeof(GameplayTickViewPresenter).Assembly));
            Assert.That(requestType.Assembly, Is.Not.EqualTo(typeof(WorldState).Assembly));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.TickIndex)));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.Semantic)));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.Variant)));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.SourceEntityId)));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.SequenceOrActionPlanId)));
            Assert.That(propertyNames, Does.Contain(nameof(CameraShakeImpulseRequest.Priority)));
            Assert.That(propertyNames, Does.Not.Contain("RequestKey"));
            Assert.That(propertyNames, Does.Not.Contain("DelaySeconds"));
            Assert.That(propertyNames, Does.Not.Contain("AmplitudeScale"));
            Assert.That(propertyNames, Does.Not.Contain("DurationScale"));
            Assert.That(propertyNames, Does.Not.Contain("RotationScale"));
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeProfile_IsValidatedScriptableObjectWithSingleProductionAsset()
        {
            Assert.That(typeof(GameplayCameraShakeProfile).IsSubclassOf(typeof(ScriptableObject)), Is.True);
            Assert.That(typeof(GameplayCameraShakeProfile).GetMethod(nameof(GameplayCameraShakeProfile.ValidateOrThrow)), Is.Not.Null);
            var profileGuids = AssetDatabase.FindAssets("t:GameplayCameraShakeProfile");
            Assert.That(profileGuids, Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.GUIDToAssetPath(profileGuids[0]), Is.EqualTo(ProductionProfilePath));
            var profile = AssetDatabase.LoadAssetAtPath<GameplayCameraShakeProfile>(ProductionProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.DoesNotThrow(profile.ValidateOrThrow);
        }

        [Test]
        [Category("Extended")]
        public void ImpulseEvaluator_Source_HasNoRandomOrClockDependency()
        {
            var source = ReadRepoFile($"{FeedbackDirectory}/CameraShakeImpulseEvaluator.cs");
            var forbidden = new[]
            {
                "UnityEngine.Random",
                "System.Random",
                "Random.Range",
                "Random.insideUnitSphere",
                "Time.time",
                "Time.realtimeSinceStartup",
                "Time.frameCount",
            };

            foreach (var fragment in forbidden)
            {
                Assert.That(source, Does.Not.Contain(fragment));
            }

            Assert.That(source, Does.Contain("CreateStableSeed"));
            Assert.That(source, Does.Contain("System.Math.Sin"));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_OwnsArbitrationWhileRigRemainsSemanticFreeFinalWriter()
        {
            var mixerSource = ReadRepoFile($"{FeedbackDirectory}/GameplayCameraShakeMixer.cs");
            var presenterSource = ReadRepoFile(PresenterPath);
            var rigSource = ReadRepoFile(RigPath);

            Assert.That(mixerSource, Does.Contain("_activeImpulses"));
            Assert.That(mixerSource, Does.Contain("_acceptedIdentities"));
            Assert.That(mixerSource, Does.Contain("SetTopologyContribution"));
            Assert.That(mixerSource, Does.Contain("SelectAbsoluteDominant"));
            Assert.That(mixerSource, Does.Contain("SetMotionLevel"));
            Assert.That(presenterSource, Does.Contain("private readonly GameplayCameraShakeMixer _cameraShakeMixer"));
            Assert.That(presenterSource, Does.Contain("IGameplayCameraAdditivePosePort"));
            Assert.That(rigSource, Does.Contain("ApplyCachedAdditivePoseToHierarchy"));
            Assert.That(rigSource, Does.Not.Contain("GameplayCameraShakeMixer"));

            foreach (var semantic in System.Enum.GetNames(typeof(CameraShakeSemantic)))
            {
                Assert.That(rigSource, Does.Not.Contain(semantic));
            }

            foreach (var variant in System.Enum.GetNames(typeof(CameraShakeVariant))
                         .Where(name => name != nameof(CameraShakeVariant.Default)))
            {
                Assert.That(rigSource, Does.Not.Contain(variant));
            }
        }

        [Test]
        [Category("Extended")]
        public void CameraFeedback_HasNoAudioCinemachineImpulseOrAuthoritativeRuntimeDependency()
        {
            var feedbackSource = string.Join(
                "\n",
                Directory.GetFiles(ResolveRepoPath(FeedbackDirectory), "*.cs", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));

            Assert.That(feedbackSource, Does.Not.Contain("Gameplay.Audio"));
            Assert.That(feedbackSource, Does.Not.Contain("Gameplay.ActionAudio"));
            Assert.That(feedbackSource, Does.Not.Contain("CinemachineImpulse"));
            Assert.That(feedbackSource, Does.Not.Contain("TickPipeline"));
            Assert.That(feedbackSource, Does.Not.Contain("DeterminismHashBuilder"));
            Assert.That(feedbackSource, Does.Not.Contain("WorldState"));
        }

        [Test]
        [Category("Extended")]
        public void ProductionGameplayAuthorityFiles_DoNotSubmitCameraShakeRequests()
        {
            var forbiddenProductionPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs",
                "Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Resolution/AttackResolver.cs",
                "Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Commit/AttackCommitter.cs",
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs",
                "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs",
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
                "Assets/_Features/Stages/Runtime/StageDefinition.cs",
            };

            foreach (var path in forbiddenProductionPaths)
            {
                var source = ReadRepoFile(path);
                Assert.That(source, Does.Not.Contain("CameraShakeImpulseRequest"), path);
                Assert.That(source, Does.Not.Contain("ICameraShakeImpulseSink"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void FeedbackSlice_ContainsAssetMetaPairsAndSingleProductionProfileAsset()
        {
            var sourceFiles = Directory.GetFiles(ResolveRepoPath(FeedbackDirectory), "*.cs");
            Assert.That(sourceFiles.Length, Is.EqualTo(5));
            foreach (var sourceFile in sourceFiles)
            {
                Assert.That(File.Exists(sourceFile + ".meta"), Is.True, sourceFile);
            }

            Assert.That(
                Directory.GetFiles(ResolveRepoPath("Assets"), "*.asset", SearchOption.AllDirectories)
                    .Count(path => Path.GetFileName(path).Contains("GameplayCameraShakeProfile")),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void ProductionPlanner_ConsumesTypedFactsAndActualMotionProgressWithoutCoreAudioVfxOrTransformPolling()
        {
            var source = ReadRepoFile($"{FeedbackDirectory}/GameplayCameraShakeProductionPlanner.cs");

            Assert.That(source, Does.Contain("BoxSlideStartPresentationSignal"));
            Assert.That(source, Does.Contain("FlipFloorImpactPresentationKind.Landing"));
            Assert.That(source, Does.Contain("FlipImpactPresentationDisposition.Stay"));
            Assert.That(source, Does.Contain("FlipImpactPresentationDisposition.DestroySelf"));
            Assert.That(source, Does.Contain("FlipFloorImpactPresentationKind.FollowThrough"));
            Assert.That(source, Does.Contain("CameraShakeVariant.FlipHostileFollowThrough"));
            Assert.That(source, Does.Contain("TickPlayerDamagePresentationSignal"));
            Assert.That(source, Does.Contain("TickPlayerDeathPresentationSignal"));
            Assert.That(source, Does.Contain("lethalPlayerEntityIds.Contains(signal.EntityId)"));
            Assert.That(source, Does.Contain("CameraShakeSemantic.PlayerDamageImpact"));
            Assert.That(source, Does.Contain("CameraShakeSemantic.PlayerLethalImpact"));
            Assert.That(source, Does.Contain("CameraShakePriority.Light"));
            Assert.That(source, Does.Contain("CameraShakePriority.Heavy"));
            Assert.That(source, Does.Contain("hasDirection: false"));
            Assert.That(source, Does.Contain("MotionTrackProgressSample"));
            Assert.That(source, Does.Contain("PreviousNormalizedTime"));
            Assert.That(source, Does.Contain("CurrentNormalizedTime"));
            Assert.That(source, Does.Contain("SequenceOrActionPlanId != sample.SequenceOrActionPlanId"));
            Assert.That(source, Does.Not.Contain("DelaySeconds"));
            Assert.That(source, Does.Not.Contain("Transform"));
            Assert.That(source, Does.Not.Contain("Physics"));
            Assert.That(source, Does.Not.Contain("Animator"));
            Assert.That(source, Does.Not.Contain("Audio"));
            Assert.That(source, Does.Not.Contain("Vfx"));
            Assert.That(source, Does.Not.Contain("VFX"));
            Assert.That(source, Does.Not.Contain("WorldState"));
            Assert.That(source, Does.Not.Contain("TickPipeline"));
            Assert.That(source, Does.Not.Contain("AmplitudeScale"));
            Assert.That(source, Does.Not.Contain("maxHp"));
            Assert.That(source, Does.Not.Contain("Time.frameCount"));
            Assert.That(source, Does.Not.Contain("GetInstanceID"));

            var pipelineSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime/GameplayPresentationPipeline.cs");
            var motionTrackSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/MotionTrack.cs");
            Assert.That(pipelineSource, Does.Contain("sourceActionPlanId: startSignal.SourceActionPlanId"));
            Assert.That(pipelineSource, Does.Contain("sourceActionPlanId: landingSignal.SourceActionPlanId"));
            Assert.That(motionTrackSource, Does.Contain("public int SequenceOrActionPlanId { get; }"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAuthorityAndRigSources_RemainFreeOfProductionPushFlipCameraSemantics()
        {
            var gameplayAuthorityPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs",
                "Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Resolution/AttackResolver.cs",
                "Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Commit/AttackCommitter.cs",
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs",
                "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs",
                "Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs",
            };

            foreach (var path in gameplayAuthorityPaths)
            {
                var source = ReadRepoFile(path);
                Assert.That(source, Does.Not.Contain("CameraShake"), path);
            }

            var rigSource = ReadRepoFile(RigPath);
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.PushSlideLaunch)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.FlipFloorLanding)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.FlipHostileImpact)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.PlayerDamageImpact)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.PlayerLethalImpact)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeSemantic.HeavyEnemyJumpLanding)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeVariant.FlipHostileStay)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeVariant.FlipHostileDestroySelf)));
            Assert.That(rigSource, Does.Not.Contain(nameof(CameraShakeVariant.FlipHostileFollowThrough)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionProfile_ContainsRequiredKeysAndPreservesM2M3AValuesWhileActivatingM3B()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameplayCameraShakeProfile>(ProductionProfilePath);
            Assert.That(profile, Is.Not.Null);
            var entries = profile.CreateValidatedEntryMap();
            Assert.That(entries, Has.Count.EqualTo(8));

            var push = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.PushSlideLaunch,
                CameraShakeVariant.Default)];
            Assert.That(push.DurationSeconds, Is.EqualTo(0.1f));
            Assert.That(push.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.004f, 0.003f, 0.006f)));
            Assert.That(push.LocalRotationAmplitudeDegrees, Is.EqualTo(new Vector3(0.12f, 0.08f, 0.04f)));

            var ordinaryFlip = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.FlipFloorLanding,
                CameraShakeVariant.Default)];
            Assert.That(ordinaryFlip.DurationSeconds, Is.EqualTo(0.15f));
            Assert.That(ordinaryFlip.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.007f, 0.005f, 0.01f)));
            Assert.That(ordinaryFlip.LocalRotationAmplitudeDegrees, Is.EqualTo(new Vector3(0.24f, 0.15f, 0.08f)));

            Assert.That(entries.Keys.Count(key => key.Semantic == CameraShakeSemantic.FlipHostileImpact),
                Is.EqualTo(3));

            var hostileStay = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.FlipHostileImpact,
                CameraShakeVariant.FlipHostileStay)];
            Assert.That(hostileStay.DurationSeconds, Is.EqualTo(0.14f));
            Assert.That(hostileStay.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.0075f, 0.0055f, 0.0105f)));

            var hostileDestroySelf = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.FlipHostileImpact,
                CameraShakeVariant.FlipHostileDestroySelf)];
            Assert.That(hostileDestroySelf.DurationSeconds, Is.EqualTo(0.16f));
            Assert.That(hostileDestroySelf.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.009f, 0.006f, 0.012f)));

            var hostileFollowThrough = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.FlipHostileImpact,
                CameraShakeVariant.FlipHostileFollowThrough)];
            Assert.That(hostileFollowThrough.DurationSeconds, Is.EqualTo(0.2f));
            Assert.That(hostileFollowThrough.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.011f, 0.007f, 0.015f)));

            var playerDamage = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.PlayerDamageImpact,
                CameraShakeVariant.Default)];
            Assert.That(playerDamage.Priority, Is.EqualTo(CameraShakePriority.Light));
            Assert.That(playerDamage.DurationSeconds, Is.EqualTo(0.11f));
            Assert.That(playerDamage.OscillationCycles, Is.EqualTo(2));
            Assert.That(playerDamage.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.0045f, 0.003f, 0.0065f)));
            Assert.That(playerDamage.LocalRotationAmplitudeDegrees, Is.EqualTo(new Vector3(0.13f, 0.08f, 0.04f)));
            Assert.That(playerDamage.AttackSeconds, Is.EqualTo(0.012f));
            Assert.That(playerDamage.CooldownSeconds, Is.EqualTo(0.12f));

            var playerLethal = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.PlayerLethalImpact,
                CameraShakeVariant.Default)];
            Assert.That(playerLethal.Priority, Is.EqualTo(CameraShakePriority.Heavy));
            Assert.That(playerLethal.DurationSeconds, Is.EqualTo(0.2f));
            Assert.That(playerLethal.OscillationCycles, Is.EqualTo(2));
            Assert.That(playerLethal.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.01f, 0.007f, 0.014f)));
            Assert.That(playerLethal.LocalRotationAmplitudeDegrees, Is.EqualTo(new Vector3(0.34f, 0.22f, 0.12f)));
            Assert.That(playerLethal.AttackSeconds, Is.EqualTo(0.015f));
            Assert.That(playerLethal.CooldownSeconds, Is.EqualTo(0.1f));

            var heavyEnemyLanding = entries[new CameraShakeProfileKey(
                CameraShakeSemantic.HeavyEnemyJumpLanding,
                CameraShakeVariant.Default)];
            Assert.That(heavyEnemyLanding.Priority, Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(heavyEnemyLanding.DurationSeconds, Is.EqualTo(0.16f));
            Assert.That(heavyEnemyLanding.OscillationCycles, Is.EqualTo(2));
            Assert.That(heavyEnemyLanding.LocalPositionAmplitude, Is.EqualTo(new Vector3(0.008f, 0.006f, 0.011f)));
            Assert.That(heavyEnemyLanding.LocalRotationAmplitudeDegrees, Is.EqualTo(new Vector3(0.26f, 0.17f, 0.09f)));
            Assert.That(heavyEnemyLanding.AttackSeconds, Is.EqualTo(0.02f));
            Assert.That(heavyEnemyLanding.CooldownSeconds, Is.EqualTo(0.08f));
        }

        [Test]
        [Category("Extended")]
        public void ProductionProfile_AllRequiredImpulses_FirstLifetimeCrossingProducesVisibleSampleThenExpires()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameplayCameraShakeProfile>(ProductionProfilePath);
            Assert.That(profile, Is.Not.Null);
            var entries = profile.CreateValidatedEntryMap()
                .OrderBy(pair => pair.Key.Semantic)
                .ThenBy(pair => pair.Key.Variant)
                .ToArray();

            Assert.That(entries, Has.Length.EqualTo(8));
            for (var index = 0; index < entries.Length; index++)
            {
                var key = entries[index].Key;
                var entry = entries[index].Value;
                var mixer = new GameplayCameraShakeMixer();
                mixer.ConfigureProfile(profile);
                var request = new CameraShakeImpulseRequest(
                    tickIndex: 100 + index,
                    semantic: key.Semantic,
                    sourceEntityId: 200 + index,
                    sequenceOrActionPlanId: 300 + index,
                    priority: entry.Priority,
                    variant: key.Variant);

                Assert.That(entry.DurationSeconds, Is.GreaterThan(0f), key.ToString());
                Assert.That(entry.AttackSeconds, Is.GreaterThan(0f), key.ToString());
                Assert.That(mixer.Submit(request), Is.True, key.ToString());
                Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero), key.ToString());

                mixer.Advance(entry.DurationSeconds);

                Assert.That(mixer.CurrentResult.IsActive, Is.True, key.ToString());
                Assert.That(
                    mixer.CurrentResult.LocalPosition.sqrMagnitude +
                    mixer.CurrentResult.LocalRotationDegrees.sqrMagnitude,
                    Is.GreaterThan(0.00000001f),
                    key.ToString());
                Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1), key.ToString());

                mixer.Advance(0f);

                Assert.That(mixer.ActiveImpulseCount, Is.Zero, key.ToString());
                Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero), key.ToString());
                Assert.That(mixer.CurrentResult.LocalRotation, Is.EqualTo(Quaternion.identity), key.ToString());
            }
        }

        [Test]
        [Category("Extended")]
        public void HeavyEnemyLandingEligibility_IsExplicitOnAstretonAndMissingAuthoringDefaultsToNone()
        {
            var astreton = AssetDatabase.LoadAssetAtPath<GameObject>(AstretonPrefabPath);
            var nonHeavyReference = AssetDatabase.LoadAssetAtPath<GameObject>(NonHeavyReferencePrefabPath);
            Assert.That(astreton, Is.Not.Null, AstretonPrefabPath);
            Assert.That(nonHeavyReference, Is.Not.Null, NonHeavyReferencePrefabPath);

            var heavyAuthoring = astreton.GetComponent<EnemyJumpMotionPresentationAuthoring>();
            Assert.That(heavyAuthoring, Is.Not.Null, "Astreton must explicitly author its heavy jump landing feedback.");
            Assert.DoesNotThrow(heavyAuthoring.Validate);
            Assert.That(
                heavyAuthoring.JumpLandingCameraFeedback,
                Is.EqualTo(EnemyJumpLandingCameraFeedbackKind.Heavy));
            Assert.That(
                nonHeavyReference.GetComponent<EnemyJumpMotionPresentationAuthoring>(),
                Is.Null,
                "A presentation without jump landing feedback authoring must retain the safe None behavior.");
        }

        [Test]
        [Category("Extended")]
        public void HeavyEnemyLandingAuthoringAndVisibility_RemainPresentationOnlySemanticFreeSeams()
        {
            Assert.That(
                typeof(EnemyJumpMotionPresentationAuthoring).Assembly,
                Is.EqualTo(typeof(GameplayEntityView).Assembly));
            Assert.That(typeof(EnemyJumpMotionPresentationAuthoring).IsSubclassOf(typeof(MonoBehaviour)), Is.True);

            var visibilityMethods = typeof(IGameplayCameraVisibilityPort)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(visibilityMethods, Has.Length.EqualTo(1));
            Assert.That(
                visibilityMethods.Single().Name,
                Is.EqualTo(nameof(IGameplayCameraVisibilityPort.TryProjectUnshakenWorldPoint)));
            Assert.That(typeof(GameplayCameraRig).GetInterfaces(), Has.Member(typeof(IGameplayCameraVisibilityPort)));
            Assert.That(
                string.Join("\n", visibilityMethods.Select(method => method.ToString())),
                Does.Not.Contain(nameof(CameraShakeSemantic.HeavyEnemyJumpLanding)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAiContracts_DoNotOwnCameraFeedbackAuthoring()
        {
            foreach (var type in new[] { typeof(EnemyAiProfile), typeof(EnemyAiRuntimeDefinition) })
            {
                var memberNames = type
                    .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(member => member.Name)
                    .ToArray();
                Assert.That(
                    memberNames.Any(name => name.IndexOf("Camera", System.StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    type.FullName);
                Assert.That(
                    memberNames.Any(name => name.IndexOf("Shake", System.StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    type.FullName);
                Assert.That(
                    memberNames.Any(name => name.IndexOf("Feedback", System.StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False,
                    type.FullName);
            }

            var gameplayAuthorityPaths = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyGlideExecutor.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemySummonExecutor.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyAiProfile.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyAiConfig.cs",
            };
            foreach (var path in gameplayAuthorityPaths)
            {
                var source = ReadRepoFile(path);
                Assert.That(source, Does.Not.Contain(nameof(CameraShakeSemantic.HeavyEnemyJumpLanding)), path);
                Assert.That(source, Does.Not.Contain(nameof(EnemyJumpLandingCameraFeedbackKind)), path);
                Assert.That(source, Does.Not.Contain("CameraShakeImpulseRequest"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void HeavyEnemyLandingPlanner_UsesTypedOutcomePresenterMilestoneAndVisibilityWithoutTimingPolls()
        {
            var plannerSource = ReadRepoFile($"{FeedbackDirectory}/GameplayCameraShakeProductionPlanner.cs");
            var presenterSource = ReadRepoFile(PresenterPath);

            Assert.That(plannerSource, Does.Contain("TickEnemyJumpPresentationSignal"));
            Assert.That(plannerSource, Does.Contain("TickEnemyJumpPresentationOutcome.Landed"));
            Assert.That(plannerSource, Does.Contain("TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded"));
            Assert.That(plannerSource, Does.Contain("EnemyJumpLandingCameraFeedbackKind.Heavy"));
            Assert.That(plannerSource, Does.Contain("ObserveHeavyEnemyJumpLandingMilestones"));
            Assert.That(plannerSource, Does.Contain("TryProjectUnshakenWorldPoint"));
            Assert.That(plannerSource, Does.Not.Contain("DelaySeconds"));
            Assert.That(plannerSource, Does.Not.Contain("GetInstanceID"));
            Assert.That(plannerSource, Does.Not.Contain("Time.time"));
            Assert.That(plannerSource, Does.Not.Contain("Physics"));
            Assert.That(plannerSource, Does.Not.Contain("Collider"));
            Assert.That(plannerSource, Does.Not.Contain("Animator"));
            Assert.That(presenterSource, Does.Contain("HasActiveJumpLandingCompletionTrack"));
            Assert.That(presenterSource, Does.Contain("ResolveEnemyPresentationAnchor"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerLethalPromotionAndTerminalReset_RemainPresentationOwned()
        {
            var plannerSource = ReadRepoFile($"{FeedbackDirectory}/GameplayCameraShakeProductionPlanner.cs");
            var campaignFlowSource = ReadRepoFile(CampaignFlowPath);
            var presenterSource = ReadRepoFile(PresenterPath);

            Assert.That(plannerSource, Does.Contain("PresentPlayerImpacts"));
            Assert.That(plannerSource, Does.Contain("lethalPlayerEntityIds"));
            Assert.That(campaignFlowSource, Does.Contain("TerminalTransitionState.Closing"));
            Assert.That(campaignFlowSource, Does.Contain("CompleteStageTerminalCameraHandoff"));
            Assert.That(presenterSource, Does.Contain("_cameraShakeMixer.HardReset()"));
            Assert.That(presenterSource, Does.Contain("ResetCameraAdditivePose()"));
            Assert.That(plannerSource, Does.Not.Contain("StageResult"));
            Assert.That(plannerSource, Does.Not.Contain("TerminalTransition"));
        }

        [Test]
        [Category("Extended")]
        public void ProductionProfile_IsBoundByBothCampaignCameraPresetsAndBootstrap()
        {
            var expectedProfile = AssetDatabase.LoadAssetAtPath<GameplayCameraShakeProfile>(ProductionProfilePath);
            Assert.That(expectedProfile, Is.Not.Null);
            var presetPaths = new[]
            {
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Topology/CameraProfiles/GameplayCameraTopologyPreset_CampaignMainQualityPostFx.asset",
                "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Topology/CameraProfiles/GameplayCameraTopologyPreset_CampaignMainFastPostFx.asset",
            };

            foreach (var presetPath in presetPaths)
            {
                var preset = AssetDatabase.LoadAssetAtPath<GameplayCameraTopologyPreset>(presetPath);
                Assert.That(preset, Is.Not.Null, presetPath);
                var serializedPreset = new SerializedObject(preset);
                var profileProperty = serializedPreset.FindProperty("sharedTuning.gameplayCameraShakeProfile");
                Assert.That(profileProperty, Is.Not.Null, presetPath);
                Assert.That(profileProperty.objectReferenceValue, Is.SameAs(expectedProfile), presetPath);
            }

            var composerSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraTopologyConfigurationComposer.cs");
            var startupSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayCameraStartupPlanComposer.cs");
            var bootstrapSource = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Bootstrap/GameplayHostTopologyVisualRuntimeBootstrap.cs");
            Assert.That(composerSource, Does.Contain("configuration.GameplayCameraShakeProfile = sharedTuning.GameplayCameraShakeProfile"));
            Assert.That(startupSource, Does.Contain("configuration.GameplayCameraShakeProfile"));
            Assert.That(bootstrapSource, Does.Contain("ConfigureGameplayCameraShakeProfile(startupPlan.GameplayCameraShakeProfile)"));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(ResolveRepoPath(relativePath));
        }

        private static string ResolveRepoPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
