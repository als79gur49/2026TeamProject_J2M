using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PlayerLocomotionAudio;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.TileFeatureAudio;
using Game.Feature.Gameplay.TopologyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class GameplayAudioIntegrationPlayModeTests
    {
        private const string PlayerPrefabPath = "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab";
        private const string PlayerActionAudioProfilePath =
            "Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset";
        private const string RocketFacePrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_RocketFace.prefab";
        private const string RocketFaceEnemyAudioProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_RocketFace.asset";
        private const string RocketFaceEnemyAudioRequirementBindingPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioRequirementBindings/EnemyAudioRequirementBinding_RocketFace.asset";
        private const string ChargeLoopEnemyAudioRequirementPolicyPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioRequirementPolicies/EnemyAudioRequirementPolicy_ChargeLoop.asset";
        private const string DrSaturnPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab";
        private const string NebulousPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Nebulous.prefab";
        private const string JPeterPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab";
        private const string EnemyPresentationArchetypeCatalogPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Catalogs/EnemyPresentationArchetypeCatalog_CampaignMainEnemy.asset";
        private const string PassiveContactMinionArchetypeId = "PassiveContactMinion";

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_GameplaySfx_RespectsMasterAndSfxMix_AndIgnoresBgmMix()
        {
            var context = CreateHostContext(nameof(GameplaySceneHost_GameplaySfx_RespectsMasterAndSfxMix_AndIgnoresBgmMix));
            try
            {
                var settings = context.Installer.AudioSettingsService;

                settings.SetChannelVolume(AudioChannel.Master, 0.8f);
                settings.SetChannelVolume(AudioChannel.Sfx, 0.5f);
                settings.SetChannelVolume(AudioChannel.Bgm, 0.1f);

                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                yield return null;

                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelVolume(AudioChannel.Bgm, 0f);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelMuted(AudioChannel.Sfx, true);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));

                settings.SetChannelMuted(AudioChannel.Sfx, false);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.4f).Within(0.0001f));

                settings.SetChannelVolume(AudioChannel.Master, 0.25f);
                Assert.That(snapshots[0].Source.volume, Is.EqualTo(0.125f).Within(0.0001f));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_GameplaySfx_SettingsStayBounded_AndDoNotReplayConsumedPlan()
        {
            var persistenceStore = new RecordingAudioSettingsPersistenceStore();
            var context = CreateHostContext(
                nameof(GameplaySceneHost_GameplaySfx_SettingsStayBounded_AndDoNotReplayConsumedPlan),
                persistenceStore);
            try
            {
                var settings = context.Installer.AudioSettingsService;

                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10)));
                yield return null;

                var initialSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(initialSnapshots, Has.Length.EqualTo(1));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelVolume(AudioChannel.Sfx, 0.3f);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelMuted(AudioChannel.Sfx, true);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                settings.SetChannelMuted(AudioChannel.Sfx, false);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.Zero);

                context.Host.Presenter.Present(CreateTickResult(TickPresentationData.Empty));
                yield return null;

                var replayGuardSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(replayGuardSnapshots, Has.Length.EqualTo(1));
                Assert.That(replayGuardSnapshots[0].Source, Is.SameAs(initialSnapshots[0].Source));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                settings.FlushSettings();
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(1));

                settings.SetChannelVolume(AudioChannel.Master, 0.5f);
                Assert.That(initialSnapshots[0].Source.volume, Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(persistenceStore.SaveCallCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_ActionAudioProfile_PlaybackReachesAudioService()
        {
            var trackedObjects = new List<UnityEngine.Object>();
            var actionProfile = CreateActionAudioProfile(trackedObjects);
            var playerEntity = CreatePlayerEntityState();
            var context = CreateActionAudioHostContext(
                nameof(GameplaySceneHost_ActionAudioProfile_PlaybackReachesAudioService),
                new RuntimeActionAudioViewFactory(actionProfile),
                playerEntity);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerActionPresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: playerEntity.entityId,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 1,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    new[] { playerEntity }));
                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));

                // One-shot action SFX may naturally complete before the next frame in batchmode.
                yield return null;
            }
            finally
            {
                context.Dispose();
                for (var i = trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (trackedObjects[i] != null)
                    {
                        UnityEngine.Object.Destroy(trackedObjects[i]);
                    }
                }
            }
        }

#if UNITY_EDITOR
        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplaySceneHost_ActionAudioSerializedPlayerPrefab_ProductionBridgeReachesSharedAudioService()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null, $"Missing prefab at '{PlayerPrefabPath}'.");
            var authoring = playerPrefab.GetComponent<GameplayActionAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(GameplayActionAudioAuthoring)} on '{PlayerPrefabPath}'.");
            Assert.That(AssetDatabase.GetAssetPath(authoring.Profile), Is.EqualTo(PlayerActionAudioProfilePath));
            Assert.That(authoring.Profile.TryResolve(GameplayActionKind.Push, GameplayActionAudioMoment.Windup, out _), Is.True);

            var playerEntity = CreatePlayerEntityState();
            var context = CreateSerializedPlayerPrefabActionAudioHostContext(
                nameof(GameplaySceneHost_ActionAudioSerializedPlayerPrefab_ProductionBridgeReachesSharedAudioService),
                playerPrefab,
                playerEntity);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerActionPresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: playerEntity.entityId,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 51,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false,
                            direction: Direction.Right,
                            actionPlanId: 510)),
                    new[] { playerEntity },
                    tickIndex: 151));

                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                var telemetry = context.Host.Presenter.ActionAudioProductionTelemetrySnapshot;
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(snapshots[0].LeafChannel, Is.EqualTo(AudioChannel.Sfx));
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationActionAudioCueKey.PlayerPushWindup));
                Assert.That(telemetry.LastOwnerEntityId, Is.EqualTo(playerEntity.entityId));
                Assert.That(telemetry.LastAction, Is.EqualTo(GameplayActionKind.Push));
                Assert.That(telemetry.LastMoment, Is.EqualTo(GameplayActionAudioMoment.Windup));
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(1));
                Assert.That(telemetry.RequestPlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(1));

                yield return null;
            }
            finally
            {
                context.Dispose();
            }
        }
#endif

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplaySceneHost_ActionAudioProductionBridge_SameTickLayeringResetHardCleanupAndFallback()
        {
            var trackedObjects = new List<UnityEngine.Object>();
            var actionProfile = CreateActionAudioProfile(trackedObjects);
            var playerEntity = CreatePlayerEntityState();
            var context = CreateActionAudioHostContext(
                nameof(GameplaySceneHost_ActionAudioProductionBridge_SameTickLayeringResetHardCleanupAndFallback),
                new RuntimeActionAudioViewFactory(actionProfile),
                playerEntity);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerActionPresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: playerEntity.entityId,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 61,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false),
                        new TickPlayerActionPresentationSignal(
                            entityId: playerEntity.entityId,
                            activeActionKind: PlayerActionKind.Flip,
                            activeActionSequence: 62,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    new[] { playerEntity },
                    tickIndex: 161));

                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(2));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.ObservedCueCount, Is.EqualTo(2));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(2));

                context.Host.Presenter.PresentInitial(new[] { playerEntity }, new CubeTopologyState(FaceId.Floor));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.ActionAudioProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(ActionAudioTelemetryCleanupReason.ResetSession));

                context.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerActionPresentationData(
                        new TickPlayerActionPresentationSignal(
                            entityId: 999,
                            activeActionKind: PlayerActionKind.Push,
                            activeActionSequence: 63,
                            startedThisTick: true,
                            completedThisTick: false,
                            canceledThisTick: false)),
                    new[] { playerEntity },
                    tickIndex: 162));
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.OwnerViewMissingCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.ActionAudioProductionTelemetrySnapshot.LastFailureReason, Is.EqualTo(ActionAudioTelemetryFailureReason.OwnerViewMissing));

                context.Host.Presenter.DebugHardCleanupPresentationExtensions();
                Assert.That(context.Host.Presenter.ActionAudioExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(context.Host.Presenter.ActionAudioProductionTelemetrySnapshot.LastCleanupReason, Is.EqualTo(ActionAudioTelemetryCleanupReason.HardCleanupPresentationExtensions));

                yield return null;
            }
            finally
            {
                context.Dispose();
                for (var i = trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (trackedObjects[i] != null)
                    {
                        UnityEngine.Object.Destroy(trackedObjects[i]);
                    }
                }
            }
        }

#if UNITY_EDITOR
        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_EnemyAudioSerializedRocketFacePrefab_ProfileBindingAndChargeLoopPolicyWiring()
        {
            var rocketFacePrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(RocketFacePrefabPath);
            var rocketFaceProfile = AssetDatabase.LoadAssetAtPath<EnemyAudioProfile>(RocketFaceEnemyAudioProfilePath);
            var rocketFaceBinding =
                AssetDatabase.LoadAssetAtPath<EnemyAudioRequirementBinding>(RocketFaceEnemyAudioRequirementBindingPath);
            var chargeLoopPolicy =
                AssetDatabase.LoadAssetAtPath<EnemyAudioRequirementPolicy>(ChargeLoopEnemyAudioRequirementPolicyPath);

            Assert.That(rocketFacePrefab, Is.Not.Null, $"Missing prefab at '{RocketFacePrefabPath}'.");
            Assert.That(rocketFaceProfile, Is.Not.Null, $"Missing profile at '{RocketFaceEnemyAudioProfilePath}'.");
            Assert.That(rocketFaceBinding, Is.Not.Null, $"Missing binding at '{RocketFaceEnemyAudioRequirementBindingPath}'.");
            Assert.That(chargeLoopPolicy, Is.Not.Null, $"Missing policy at '{ChargeLoopEnemyAudioRequirementPolicyPath}'.");

            var authoring = rocketFacePrefab.GetComponent<EnemyAudioAuthoring>();
            Assert.That(authoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{RocketFacePrefabPath}'.");
            Assert.That(authoring.Profile, Is.SameAs(rocketFaceProfile));
            Assert.That(rocketFaceBinding.TargetProfile, Is.SameAs(rocketFaceProfile));
            Assert.That(rocketFaceBinding.Policy, Is.SameAs(chargeLoopPolicy));
            Assert.That(
                rocketFaceBinding.GetEffectiveRequirement(EnemyAudioCue.ChargeActiveLoop),
                Is.EqualTo(EnemyAudioCueRequirement.Required));
            Assert.That(rocketFaceProfile.TryResolve(EnemyAudioCue.ChargeActiveLoop, out var loopBinding), Is.True);
            Assert.That(loopBinding.Definition.Loop, Is.True);
            Assert.That(loopBinding.HasAttachmentSlot, Is.True);

            yield return null;
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplaySceneHost_EnemyAudioProductionBridge_ActualDrSaturnPrefab_UtilityPhasesPlayOnce()
        {
            yield return AssertActualEnemyUtilityPhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualDrSaturnPrefab_UtilityPhasesPlayOnce) + "_Windup",
                DrSaturnPrefabPath,
                EnemyUtilityPresentationPhase.WindupStarted,
                PresentationEnemyAudioCueKey.Windup,
                PresentationEnemyAudioPhase.Windup);
            yield return AssertActualEnemyUtilityPhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualDrSaturnPrefab_UtilityPhasesPlayOnce) + "_Active",
                DrSaturnPrefabPath,
                EnemyUtilityPresentationPhase.ActiveStarted,
                PresentationEnemyAudioCueKey.Active,
                PresentationEnemyAudioPhase.Active);
            yield return AssertActualEnemyUtilityPhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualDrSaturnPrefab_UtilityPhasesPlayOnce) + "_Recover",
                DrSaturnPrefabPath,
                EnemyUtilityPresentationPhase.RecoverStarted,
                PresentationEnemyAudioCueKey.Recover,
                PresentationEnemyAudioPhase.Recover);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplaySceneHost_EnemyAudioProductionBridge_ActualNebulousPrefab_GlidePhaseStartsPlayOnce()
        {
            yield return AssertActualEnemyGlidePhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualNebulousPrefab_GlidePhaseStartsPlayOnce) + "_Windup",
                EnemyGlidePhase.Windup,
                PresentationEnemyAudioCueKey.Windup,
                PresentationEnemyAudioPhase.Windup);
            yield return AssertActualEnemyGlideContinuationDoesNotReplay();
            yield return AssertActualEnemyGlidePhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualNebulousPrefab_GlidePhaseStartsPlayOnce) + "_Active",
                EnemyGlidePhase.Active,
                PresentationEnemyAudioCueKey.Active,
                PresentationEnemyAudioPhase.Active);
            yield return AssertActualEnemyGlidePhasePlayback(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualNebulousPrefab_GlidePhaseStartsPlayOnce) + "_Recover",
                EnemyGlidePhase.Recovery,
                PresentationEnemyAudioCueKey.Recover,
                PresentationEnemyAudioPhase.Recover);
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator GameplaySceneHost_EnemyAudioProductionBridge_ActualJPeterPrefab_SummonActiveUsesSourceOwner()
        {
            var summoner = CreateUnit(60, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateActualEnemyPrefabHostContext(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_ActualJPeterPrefab_SummonActiveUsesSourceOwner),
                JPeterPrefabPath,
                summoner);
            try
            {
                AssertActualEnemyPrefabAudioAuthoring(JPeterPrefabPath, context, summoner.entityId);
                AttachProductionSummonedEnemyPresentationRegistry(
                    context,
                    new EnemyUnitArchetypeId(PassiveContactMinionArchetypeId));

                context.Host.Presenter.Present(CreateTickResult(
                    CreateSummonActivePresentationData(
                        spawnedEntityId: 61,
                        sourceEntityId: summoner.entityId,
                        archetypeId: new EnemyUnitArchetypeId(PassiveContactMinionArchetypeId)),
                    new[] { summoner },
                    tickIndex: 321,
                    determinismHash: "j-peter-summon-active"));
                yield return null;

                AssertEnemyAudioProductionOneShotRoute(
                    context,
                    expectedCue: PresentationEnemyAudioCueKey.Active,
                    expectedOrigin: PresentationEnemyAudioOriginKind.Summon,
                    expectedPhase: PresentationEnemyAudioPhase.Active,
                    expectedExecutorCount: 1);
                Assert.That(context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot.LastOwnerEntityId, Is.EqualTo(summoner.entityId));

                context.Host.Presenter.Present(CreateTickResult(
                    CreateSummonActivePresentationData(spawnedEntityId: 62, sourceEntityId: 0),
                    new[] { summoner },
                    tickIndex: 322,
                    determinismHash: "j-peter-unrelated-spawn"));
                yield return null;
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);

                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyActionWindupPresentationData(summoner.entityId),
                    new[] { summoner },
                    tickIndex: 323,
                    determinismHash: "j-peter-disabled-windup"));
                yield return null;
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);
            }
            finally
            {
                context.Dispose();
            }
        }
#endif

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_EnemyAudioProductionBridge_OneShotDoesNotStartChargeLoop()
        {
            var profileBundle = CreateEnemyAudioProfileBundle(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, loop: false, hasAttachmentSlot: false));
            var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateHostContext(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_OneShotDoesNotStartChargeLoop),
                initialEntities: new[] { enemy },
                autoCreateViews: true,
                enemyAudioProfile: profileBundle.Profile);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyActionPresentationData(enemy.entityId),
                    new[] { enemy },
                    tickIndex: 201,
                    determinismHash: "enemy-audio-one-shot"));
                yield return null;

                var snapshots = context.Manager.CaptureLivePlaybackSnapshots();
                var diagnostics = context.Host.Presenter.EnemyAudioExecutorDiagnostics;
                var telemetry = context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot;
                Assert.That(diagnostics.RequestPlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(diagnostics.UnsupportedLoopSemanticCount, Is.Zero);
                Assert.That(snapshots.Where(snapshot => snapshot.Source.loop), Is.Empty);
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationEnemyAudioCueKey.Active));
                Assert.That(telemetry.LastOriginKind, Is.EqualTo(PresentationEnemyAudioOriginKind.Action));
                Assert.That(telemetry.LastPhase, Is.EqualTo(PresentationEnemyAudioPhase.Active));

                Assert.That(context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.None));
            }
            finally
            {
                context.Dispose();
                profileBundle.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_EnemyAudioChargeActiveLoop_RefreshStopCleanupAndNoBridgeLoopRoute()
        {
            var profileBundle = CreateEnemyAudioProfileBundle(
                new EnemyAudioEntrySpec(EnemyAudioCue.ChargeActiveLoop, loop: true, hasAttachmentSlot: true));
            var enemy = CreateUnit(30, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateHostContext(
                nameof(GameplaySceneHost_EnemyAudioChargeActiveLoop_RefreshStopCleanupAndNoBridgeLoopRoute),
                initialEntities: new[] { enemy },
                autoCreateViews: true,
                enemyAudioProfile: profileBundle.Profile);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyChargePresentationData(enemy.entityId, sequence: 7, active: true),
                    new[] { enemy },
                    tickIndex: 211,
                    determinismHash: "enemy-audio-loop-start"));
                yield return null;

                var startSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(startSnapshots, Has.Length.EqualTo(1));
                Assert.That(startSnapshots[0].Source.loop, Is.True);
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.UnsupportedLoopSemanticCount, Is.Zero);
                Assert.That(
                    context.Host.Presenter.EnemyAudioExecutorDiagnostics.OptionalProfileEntryMissingNoOpCount,
                    Is.EqualTo(1));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);

                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyChargePresentationData(enemy.entityId, sequence: 7, active: true),
                    new[] { enemy },
                    tickIndex: 212,
                    determinismHash: "enemy-audio-loop-refresh"));
                yield return null;

                var refreshSnapshots = context.Manager.CaptureLivePlaybackSnapshots();
                Assert.That(refreshSnapshots, Has.Length.EqualTo(1));
                Assert.That(refreshSnapshots[0].Source, Is.SameAs(startSnapshots[0].Source));

                context.Host.Presenter.Present(CreateTickResult(
                    TickPresentationData.Empty,
                    new[] { enemy },
                    tickIndex: 213,
                    determinismHash: "enemy-audio-loop-stop"));
                yield return null;

                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.Zero);

                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyChargePresentationData(enemy.entityId, sequence: 8, active: true),
                    new[] { enemy },
                    tickIndex: 214,
                    determinismHash: "enemy-audio-loop-hard-cleanup"));
                yield return null;
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));

                context.Host.Presenter.DebugHardCleanupPresentationExtensions();
                yield return null;

                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.Zero);
                Assert.That(
                    context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot.LastCleanupReason,
                    Is.EqualTo(EnemyAudioTelemetryCleanupReason.HardCleanupPresentationExtensions));
            }
            finally
            {
                context.Dispose();
                profileBundle.Dispose();
            }
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator GameplaySceneHost_EnemyAudioProductionBridge_MissingDependencyUsesProductionDiagnosticAndNoRollback()
        {
            var profileBundle = CreateEnemyAudioProfileBundle(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, loop: false, hasAttachmentSlot: false));
            var enemy = CreateUnit(40, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateHostContext(
                nameof(GameplaySceneHost_EnemyAudioProductionBridge_MissingDependencyUsesProductionDiagnosticAndNoRollback),
                initialEntities: new[] { enemy },
                autoCreateViews: true,
                enemyAudioProfile: profileBundle.Profile);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyActionPresentationData(ownerEntityId: 999),
                    new[] { enemy },
                    tickIndex: 221,
                    determinismHash: "enemy-audio-missing-owner"));

                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.OptionalProfileEntryMissingNoOpCount, Is.EqualTo(1));
                Assert.That(
                    context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot.LastFailureReason,
                    Is.EqualTo(EnemyAudioTelemetryFailureReason.OptionalProfileEntryMissing));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.Zero);

                context.Host.Presenter.PresentInitial(new[] { enemy }, new CubeTopologyState(FaceId.Floor));
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyActionPresentationData(enemy.entityId),
                    new[] { enemy },
                    tickIndex: 222,
                    determinismHash: "enemy-audio-rollback"));
                yield return null;

                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(context.Manager.CaptureLivePlaybackSnapshots().Where(snapshot => snapshot.Source.loop), Is.Empty);
            }
            finally
            {
                context.Dispose();
                profileBundle.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxProductionDefault_PlayMode_UsesOrchestrationOwner()
        {
            var context = CreateHostContext(nameof(CoreSfxProductionDefault_PlayMode_UsesOrchestrationOwner));
            try
            {
                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 9));
                yield return null;

                var diagnostics = context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(diagnostics.CurrentMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.LegacyOwnerSkippedByPolicyCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackRequestPlannedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(context.Host.Presenter.PendingGameplayAudioRequestCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByLegacyCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback()
        {
            var context = CreateHostContext(nameof(CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback));
            try
            {
                context.Host.Presenter.Present(CreateTickResult(CreateCoreSfxPresentationData(10, 20), tickIndex: 11));
                yield return null;

                var diagnostics = context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(diagnostics.ObservedCueCount, Is.EqualTo(2));
                Assert.That(diagnostics.PlaybackRequestPlannedCount, Is.EqualTo(2));
                Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(2));
                Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByLegacyCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(2));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxProductionDefault_PlayMode_AttachedAndFallbackSmoke()
        {
            var player = CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0));
            var attachmentSlots = new Dictionary<GameplayAudioSemanticId, AudioAttachmentSlot>
            {
                { GameplayAudioSemanticId.PlayerDamage, AudioAttachmentSlot.FromId("body") },
            };
            var attachedContext = CreateHostContext(
                nameof(CoreSfxProductionDefault_PlayMode_AttachedAndFallbackSmoke) + "_Attached",
                initialEntities: new[] { player },
                autoCreateViews: true,
                gameplayAudioAttachmentSlots: attachmentSlots);
            try
            {
                attachedContext.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerDamagePresentationData(player.entityId),
                    new[] { player },
                    tickIndex: 21));
                yield return null;

                var attachedDiagnostics = attachedContext.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(attachedDiagnostics.AttachedLikePlaybackCount, Is.EqualTo(1));
                Assert.That(attachedDiagnostics.TwoDFallbackPlaybackCount, Is.Zero);
                Assert.That(attachedDiagnostics.LastFallbackReason, Is.EqualTo(GameplaySfxFallbackReason.None));
            }
            finally
            {
                attachedContext.Dispose();
            }

            var fallbackContext = CreateHostContext(
                nameof(CoreSfxProductionDefault_PlayMode_AttachedAndFallbackSmoke) + "_Fallback",
                gameplayAudioAttachmentSlots: attachmentSlots);
            try
            {
                fallbackContext.Host.Presenter.Present(CreateTickResult(
                    CreatePlayerDamagePresentationData(player.entityId),
                    tickIndex: 22));
                yield return null;

                var fallbackDiagnostics = fallbackContext.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(fallbackDiagnostics.AttachedLikePlaybackCount, Is.Zero);
                Assert.That(fallbackDiagnostics.TwoDFallbackPlaybackCount, Is.EqualTo(1));
                Assert.That(fallbackDiagnostics.OwnerViewMissingCount, Is.EqualTo(1));
                Assert.That(fallbackDiagnostics.FallbackCount, Is.EqualTo(1));
                Assert.That(fallbackDiagnostics.LastFallbackReason, Is.EqualTo(GameplaySfxFallbackReason.OwnerViewMissingTwoDFallback));
                Assert.That(
                    typeof(PresentationCue).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
                Assert.That(
                    typeof(PresentationPlaybackPlan).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
                Assert.That(
                    typeof(GameplaySfxPlaybackRequest).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Select(field => field.FieldType),
                    Has.No.Member(typeof(AudioPlaybackHandle)));
            }
            finally
            {
                fallbackContext.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxProductionDefault_PlayMode_TopologyLockDefersAndDrains()
        {
            var context = CreateHostContext(nameof(CoreSfxProductionDefault_PlayMode_TopologyLockDefersAndDrains));
            try
            {
                context.Host.Presenter.Present(CreateTickResult(CreateTopologyLockedCoreSfxPresentationData(), tickIndex: 31));
                yield return null;

                var lockedDiagnostics = context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.True);
                Assert.That(context.Host.Presenter.DeferredGameplayAudioRequestCount, Is.EqualTo(2));
                Assert.That(lockedDiagnostics.DeferredDuringTopologyLockCount, Is.EqualTo(2));
                Assert.That(lockedDiagnostics.DeferredDrainCount, Is.Zero);
                Assert.That(lockedDiagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(lockedDiagnostics.PlaybackRequestPlannedCount, Is.EqualTo(3));
                Assert.That(lockedDiagnostics.PlaybackRequestedCount, Is.EqualTo(3));

                context.Host.Presenter.UpdatePresentation(context.Host.TimingProfile.TopologyMotionDurationSeconds);
                yield return null;

                var drainedDiagnostics = context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
                Assert.That(context.Host.Presenter.DeferredGameplayAudioRequestCount, Is.Zero);
                Assert.That(drainedDiagnostics.DeferredDuringTopologyLockCount, Is.EqualTo(2));
                Assert.That(drainedDiagnostics.DeferredDrainCount, Is.EqualTo(2));
                Assert.That(drainedDiagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(drainedDiagnostics.PlaybackRequestPlannedCount, Is.EqualTo(3));
                Assert.That(drainedDiagnostics.PlaybackRequestedCount, Is.EqualTo(3));
                Assert.That(drainedDiagnostics.PlaybackSucceededCount, Is.EqualTo(3));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfxProductionDefault_PlayMode_EnemyDeathSuppressionSmoke()
        {
            var enemy = CreateUnit(20, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var profileBundle = CreateEnemyDeathAudioProfile();
            try
            {
                var lethalContext = CreateHostContext(
                    nameof(CoreSfxProductionDefault_PlayMode_EnemyDeathSuppressionSmoke) + "_Lethal",
                    initialEntities: new[] { enemy },
                    autoCreateViews: true,
                    enemyAudioProfile: profileBundle.Profile);
                try
                {
                    lethalContext.Host.Presenter.Present(CreateTickResult(
                        CreateEnemyDamageAndDeathPresentationData(enemy.entityId),
                        tickIndex: 41));
                    yield return null;

                    var diagnostics = lethalContext.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                    Assert.That(diagnostics.EnemyDeathGenericCoreSfxSuppressedCount, Is.EqualTo(1));
                    Assert.That(diagnostics.LethalEnemyDamageSuppressedByDeathCount, Is.EqualTo(1));
                    Assert.That(diagnostics.PlaybackNoOpFallbackCount, Is.EqualTo(2));
                    Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                    Assert.That(lethalContext.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                }
                finally
                {
                    lethalContext.Dispose();
                }

                var nonLethalContext = CreateHostContext(
                    nameof(CoreSfxProductionDefault_PlayMode_EnemyDeathSuppressionSmoke) + "_NonLethal",
                    initialEntities: new[] { enemy },
                    autoCreateViews: true,
                    enemyAudioProfile: profileBundle.Profile);
                try
                {
                    nonLethalContext.Host.Presenter.Present(CreateTickResult(
                        CreateEnemyDamagePresentationData(enemy.entityId),
                        new[] { enemy },
                        tickIndex: 42));
                    yield return null;

                    var diagnostics = nonLethalContext.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                    Assert.That(diagnostics.LethalEnemyDamageSuppressedByDeathCount, Is.Zero);
                    Assert.That(diagnostics.EnemyDeathGenericCoreSfxSuppressedCount, Is.Zero);
                    Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                    Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(1));
                    Assert.That(nonLethalContext.Manager.CaptureLivePlaybackCount(), Is.EqualTo(1));
                }
                finally
                {
                    nonLethalContext.Dispose();
                }
            }
            finally
            {
                profileBundle.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfx_ExplicitLegacyRollback_RemainsAvailableAfterPlayModeSmoke()
        {
            var context = CreateHostContext(
                nameof(CoreSfx_ExplicitLegacyRollback_RemainsAvailableAfterPlayModeSmoke),
                coreSfxExecutionMode: CoreGameplaySfxExecutionMode.LegacyGameplayAudioController);
            try
            {
                context.Host.Presenter.Present(CreateTickResult(CreateAllCoreSfxPresentationData(), tickIndex: 51));
                yield return null;

                var diagnostics = context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics;
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
                Assert.That(diagnostics.CurrentMode, Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
                Assert.That(diagnostics.IsProductionDefaultOwner, Is.False);
                Assert.That(diagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(diagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByLegacyCount, Is.EqualTo(6));
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.Zero);
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.GreaterThanOrEqualTo(5));
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator CoreSfx_PlayModeSmoke_IsNonAuthoritative()
        {
            var context = CreateHostContext(nameof(CoreSfx_PlayModeSmoke_IsNonAuthoritative));
            try
            {
                var finalEntities = new[]
                {
                    CreateUnit(10, UnitRole.Player, new SurfaceCell(FaceId.Floor, 0, 0)),
                };
                var eventLog = new[] { "BeforePresentation" };
                var result = CreateTickResult(
                    CreateAllCoreSfxPresentationData(),
                    finalEntities,
                    tickIndex: 61,
                    eventLog: eventLog,
                    determinismHash: "hash-before");

                context.Host.Presenter.Present(result);
                yield return null;

                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(result.DeterminismHash, Is.EqualTo("hash-before"));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.EqualTo(StageObjectiveTickResult.NoObjective));
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasPlannedBlockingBarrier, Is.False);
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.HasBlockingPresentation, Is.False);
                Assert.That(context.Host.Presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                context.Dispose();
            }
        }

        [UnityTest]
        [Category("Core")]
        public IEnumerator AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated()
        {
            var context = CreateHostContext(nameof(AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated));
            try
            {
                Assert.That(context.Host.Presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(context.Host.GetComponent<Game.Feature.Flow.Audio.GlobalAudioFlowBootstrap>(), Is.Null);
                context.Host.Presenter.Present(CreateTickResult(CreatePlayerDamagePresentationData(10), tickIndex: 71));
                yield return null;

                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.Zero);
                Assert.That(context.Host.Presenter.CoreGameplaySfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                context.Dispose();
            }
        }

        private static HostAudioIntegrationContext CreateHostContext(
            string rootName,
            RecordingAudioSettingsPersistenceStore persistenceStore = null,
            IReadOnlyList<EntityState> initialEntities = null,
            bool autoCreateViews = false,
            IReadOnlyDictionary<GameplayAudioSemanticId, AudioAttachmentSlot> gameplayAudioAttachmentSlots = null,
            EnemyAudioProfile enemyAudioProfile = null,
            CoreGameplaySfxExecutionMode? coreSfxExecutionMode = null,
            IGameplayEntityViewFactory viewFactoryOverride = null)
        {
            persistenceStore ??= new RecordingAudioSettingsPersistenceStore();
            initialEntities ??= Array.Empty<EntityState>();

            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);

            var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);

            var runtimeRootObject = new GameObject("AudioRuntimeRoot");
            runtimeRootObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            var manager = runtimeRootObject.AddComponent<AudioManager>();
            manager.SetPersistenceStoreOverrideForTesting(persistenceStore);

            var host = hostObject.AddComponent<GameplaySceneHost>();
            var mapBundle = CreateGameplayAudioMapBundle(gameplayAudioAttachmentSlots);
            var viewFactory = viewFactoryOverride ??
                              (autoCreateViews
                                  ? new RuntimeCoreSfxViewFactory(hostObject.transform, enemyAudioProfile)
                                  : null);

            hostObject.SetActive(true);
            host.Initialize(CreateHostConfiguration(
                mapBundle.Config,
                initialEntities,
                autoCreateViews,
                viewFactory));
            if (coreSfxExecutionMode.HasValue)
            {
                host.Presenter.ConfigureCoreGameplaySfxExecution(coreSfxExecutionMode.Value);
            }

            Assert.That(installer.RuntimeRoot, Is.SameAs(runtimeRoot));
            Assert.That(installer.AudioService, Is.Not.Null);
            Assert.That(installer.AudioSettingsService, Is.Not.Null);

            return new HostAudioIntegrationContext(hostObject, host, installer, manager, mapBundle, persistenceStore);
        }

#if UNITY_EDITOR
        private static HostAudioIntegrationContext CreateActualEnemyPrefabHostContext(
            string rootName,
            string prefabPath,
            EntityState enemy)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing production enemy prefab at '{prefabPath}'.");
            var hostRoot = new GameObject($"{rootName}_ViewFactoryRoot");
            var viewFactory = new DefaultGameplayEntityViewFactory(
                hostRoot.transform,
                cellSize: 1f,
                playerEntityId: 10,
                enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                {
                    { enemy.entityId, prefab },
                });
            var context = CreateHostContext(
                rootName,
                initialEntities: new[] { enemy },
                autoCreateViews: true,
                viewFactoryOverride: viewFactory);
            hostRoot.transform.SetParent(context.Host.transform, worldPositionStays: false);
            return context;
        }

        private static void AssertActualEnemyPrefabAudioAuthoring(
            string prefabPath,
            HostAudioIntegrationContext context,
            int enemyEntityId)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing production enemy prefab at '{prefabPath}'.");

            var prefabAuthoring = prefab.GetComponent<EnemyAudioAuthoring>();
            Assert.That(prefabAuthoring, Is.Not.Null, $"Missing {nameof(EnemyAudioAuthoring)} on '{prefabPath}'.");
            Assert.That(prefabAuthoring.Profile, Is.Not.Null, $"{prefabPath}: missing production audio profile.");
            Assert.DoesNotThrow(() => prefabAuthoring.Validate());

            var instance = context.Host.GetComponentsInChildren<GameplayEntityView>(includeInactive: true)
                .FirstOrDefault(view => view.EntityId == enemyEntityId);
            Assert.That(instance, Is.Not.Null, $"Missing runtime view instance for entity {enemyEntityId}.");

            var instanceAuthoring = instance.GetComponent<EnemyAudioAuthoring>();
            Assert.That(instanceAuthoring, Is.Not.Null, $"Runtime instance missing {nameof(EnemyAudioAuthoring)}.");
            Assert.That(instanceAuthoring.Profile, Is.SameAs(prefabAuthoring.Profile));
        }

        private static void AttachProductionSummonedEnemyPresentationRegistry(
            HostAudioIntegrationContext context,
            EnemyUnitArchetypeId archetypeId)
        {
            var registry = CreateProductionSummonedEnemyPresentationRegistry(archetypeId);
            var presenter = context.Host.Presenter;
            var coordinator = GetInstanceFieldValue(typeof(GameplayTickViewPresenter), presenter, "_presentationCoordinator");
            Assert.That(coordinator, Is.Not.Null, "GameplayTickViewPresenter must be bound to a coordinator.");

            var resolver = GetInstanceFieldValue(
                typeof(GameplayTickPresentationCoordinator),
                coordinator,
                "_summonedEnemyPresentationResolver");
            Assert.That(resolver, Is.Not.Null, "GameplayTickPresentationCoordinator must own a summoned enemy resolver.");
            SetSerializedField(resolver.GetType(), resolver, "_registry", registry);
        }

        private static EnemyPresentationArchetypeRegistry CreateProductionSummonedEnemyPresentationRegistry(
            EnemyUnitArchetypeId archetypeId)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationArchetypeCatalog>(EnemyPresentationArchetypeCatalogPath);
            Assert.That(
                catalog,
                Is.Not.Null,
                $"Missing production enemy presentation archetype catalog at '{EnemyPresentationArchetypeCatalogPath}'.");

            for (var i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                Assert.That(entry, Is.Not.Null, "Production enemy presentation archetype catalog cannot contain null entries.");
                if (!entry.ArchetypeId.Equals(archetypeId))
                {
                    continue;
                }

                entry.ValidateConfiguration(nameof(CreateProductionSummonedEnemyPresentationRegistry));
                return new EnemyPresentationArchetypeRegistry(
                    new Dictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime>(EnemyUnitArchetypeId.EqualityComparer)
                    {
                        { archetypeId, new EnemyPresentationArchetypeRuntime(archetypeId, entry.ViewPrefab) },
                    });
            }

            Assert.Fail(
                $"Production enemy presentation archetype catalog '{EnemyPresentationArchetypeCatalogPath}' is missing '{archetypeId}'.");
            return null;
        }

        private static void AssertEnemyAudioProductionOneShotRoute(
            HostAudioIntegrationContext context,
            PresentationEnemyAudioCueKey expectedCue,
            PresentationEnemyAudioOriginKind expectedOrigin,
            PresentationEnemyAudioPhase expectedPhase,
            int expectedExecutorCount)
        {
            var diagnostics = context.Host.Presenter.EnemyAudioExecutorDiagnostics;
            var telemetry = context.Host.Presenter.EnemyAudioProductionTelemetrySnapshot;

            Assert.That(diagnostics.PlaybackRequestedCount, Is.EqualTo(expectedExecutorCount));
            Assert.That(diagnostics.PlaybackSucceededCount, Is.EqualTo(expectedExecutorCount));
            Assert.That(diagnostics.PortMissingCount, Is.Zero);
            Assert.That(diagnostics.LastFailureReason, Is.EqualTo(EnemyAudioTelemetryFailureReason.None));
            Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.GreaterThanOrEqualTo(expectedExecutorCount));
            Assert.That(telemetry.LastCueKey, Is.EqualTo(expectedCue));
            Assert.That(telemetry.LastOriginKind, Is.EqualTo(expectedOrigin));
            Assert.That(telemetry.LastPhase, Is.EqualTo(expectedPhase));
        }

        private static IEnumerator AssertActualEnemyUtilityPhasePlayback(
            string rootName,
            string prefabPath,
            EnemyUtilityPresentationPhase utilityPhase,
            PresentationEnemyAudioCueKey expectedCue,
            PresentationEnemyAudioPhase expectedPhase)
        {
            var enemy = CreateUnit(60, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateActualEnemyPrefabHostContext(rootName, prefabPath, enemy);
            try
            {
                AssertActualEnemyPrefabAudioAuthoring(prefabPath, context, enemy.entityId);
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyUtilityPresentationData(
                        enemy.entityId,
                        utilityPhase,
                        startTick: 301,
                        executeTick: 302),
                    new[] { enemy },
                    tickIndex: 301,
                    determinismHash: rootName));
                yield return null;

                AssertEnemyAudioProductionOneShotRoute(
                    context,
                    expectedCue,
                    PresentationEnemyAudioOriginKind.Utility,
                    expectedPhase,
                    expectedExecutorCount: 1);
            }
            finally
            {
                context.Dispose();
            }
        }

        private static IEnumerator AssertActualEnemyGlidePhasePlayback(
            string rootName,
            EnemyGlidePhase glidePhase,
            PresentationEnemyAudioCueKey expectedCue,
            PresentationEnemyAudioPhase expectedPhase)
        {
            var enemy = CreateUnit(60, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateActualEnemyPrefabHostContext(rootName, NebulousPrefabPath, enemy);
            try
            {
                AssertActualEnemyPrefabAudioAuthoring(NebulousPrefabPath, context, enemy.entityId);
                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyGlidePresentationData(enemy.entityId, glidePhase, phaseElapsedTicks: 0),
                    new[] { enemy },
                    tickIndex: 311,
                    determinismHash: rootName));
                yield return null;

                AssertEnemyAudioProductionOneShotRoute(
                    context,
                    expectedCue,
                    PresentationEnemyAudioOriginKind.Glide,
                    expectedPhase,
                    expectedExecutorCount: 1);
            }
            finally
            {
                context.Dispose();
            }
        }

        private static IEnumerator AssertActualEnemyGlideContinuationDoesNotReplay()
        {
            var enemy = CreateUnit(60, UnitRole.Enemy, new SurfaceCell(FaceId.Floor, 0, 0));
            var context = CreateActualEnemyPrefabHostContext(
                nameof(AssertActualEnemyGlideContinuationDoesNotReplay),
                NebulousPrefabPath,
                enemy);
            try
            {
                AssertActualEnemyPrefabAudioAuthoring(NebulousPrefabPath, context, enemy.entityId);
                var requestedBefore = context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackRequestedCount;
                var succeededBefore = context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount;
                var liveBefore = context.Manager.CaptureLivePlaybackCount();

                context.Host.Presenter.Present(CreateTickResult(
                    CreateEnemyGlidePresentationData(
                        enemy.entityId,
                        EnemyGlidePhase.Windup,
                        phaseElapsedTicks: 1,
                        includeNonMoveMotion: true),
                    new[] { enemy },
                    tickIndex: 312,
                    determinismHash: "nebulous-windup-continuation"));
                yield return null;

                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(requestedBefore));
                Assert.That(context.Host.Presenter.EnemyAudioExecutorDiagnostics.PlaybackSucceededCount, Is.EqualTo(succeededBefore));
                Assert.That(context.Manager.CaptureLivePlaybackCount(), Is.EqualTo(liveBefore));
            }
            finally
            {
                context.Dispose();
            }
        }
#endif

        private static HostAudioIntegrationContext CreateActionAudioHostContext(
            string rootName,
            IGameplayEntityViewFactory viewFactory,
            EntityState playerEntity)
        {
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);

            var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);

            var runtimeRootObject = new GameObject("AudioRuntimeRoot");
            runtimeRootObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            var manager = runtimeRootObject.AddComponent<AudioManager>();

            var host = hostObject.AddComponent<GameplaySceneHost>();
            var mapBundle = CreateGameplayAudioMapBundle();

            hostObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = new[] { playerEntity },
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayPresentationAudioConfig = mapBundle.Config,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = viewFactory,
            });

            Assert.That(installer.RuntimeRoot, Is.SameAs(runtimeRoot));
            return new HostAudioIntegrationContext(hostObject, host, installer, manager, mapBundle, new RecordingAudioSettingsPersistenceStore());
        }

#if UNITY_EDITOR
        private static HostAudioIntegrationContext CreateSerializedPlayerPrefabActionAudioHostContext(
            string rootName,
            GameplayEntityView playerViewPrefab,
            EntityState playerEntity)
        {
            var hostObject = new GameObject(rootName);
            hostObject.SetActive(false);

            var installer = hostObject.AddComponent<AudioRuntimeInstaller>();
            SetSerializedField(typeof(AudioRuntimeInstaller), installer, "installOnAwake", false);

            var runtimeRootObject = new GameObject("AudioRuntimeRoot");
            runtimeRootObject.transform.SetParent(hostObject.transform, worldPositionStays: false);
            var runtimeRoot = runtimeRootObject.AddComponent<AudioRuntimeRoot>();
            var manager = runtimeRootObject.AddComponent<AudioManager>();

            var host = hostObject.AddComponent<GameplaySceneHost>();
            var mapBundle = CreateGameplayAudioMapBundle();

            hostObject.SetActive(true);
            host.Initialize(new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = true,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                InitialEntities = new[] { playerEntity },
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayPresentationAudioConfig = mapBundle.Config,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = new DefaultGameplayEntityViewFactory(
                    hostObject.transform,
                    1f,
                    playerEntity.entityId,
                    playerViewPrefab),
            });

            Assert.That(installer.RuntimeRoot, Is.SameAs(runtimeRoot));
            return new HostAudioIntegrationContext(hostObject, host, installer, manager, mapBundle, new RecordingAudioSettingsPersistenceStore());
        }
#endif

        private static GameplaySceneHostConfiguration CreateHostConfiguration(
            GameplayPresentationAudioConfig audioConfig,
            IReadOnlyList<EntityState> initialEntities = null,
            bool autoCreateViews = false,
            IGameplayEntityViewFactory viewFactory = null)
        {
            return new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = autoCreateViews,
                InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = (initialEntities ?? Array.Empty<EntityState>()).ToArray(),
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                GameplayPresentationAudioConfig = audioConfig,
                TopologyTransitionPostFxProfile = TopologyTransitionPostFxProfile.CreateDefault(),
                ViewFactory = viewFactory,
            };
        }

        private static TickPresentationData CreatePlayerDamagePresentationData(int entityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(entityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreatePlayerActionPresentationData(params TickPlayerActionPresentationSignal[] playerActionSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyDamagePresentationData(int enemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyDamagePresentationSignal(enemyEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyDamageAndDeathPresentationData(int enemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyDamagePresentationSignal(enemyEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        enemyEntityId,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                });
        }

        private static TickPresentationData CreateEnemyActionPresentationData(int ownerEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyActionPresentationSignal(
                        ownerEntityId,
                        EnemyActionKind.Melee,
                        activeActionSequence: 3,
                        startedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        startedRecoveryThisTick: false,
                        EnemyActionPresentationSource.Combat,
                        EnemyActionPresentationOutcome.Executed),
                },
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyChargePresentationData(
            int ownerEntityId,
            int sequence,
            bool active)
        {
            var chargeSignals = new[]
            {
                new TickEnemyChargePresentationSignal(
                    ownerEntityId,
                    sequence,
                    active ? EnemyChargePhase.Active : EnemyChargePhase.Recover,
                    startedWindupThisTick: false,
                    startedActiveThisTick: active,
                    startedRecoverThisTick: !active,
                    lockedDirection: Direction.Right),
            };
            var data = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
            SetSerializedField(
                typeof(TickPresentationData),
                data,
                "_enemyChargeSignals",
                new ReadOnlyCollection<TickEnemyChargePresentationSignal>(
                    new List<TickEnemyChargePresentationSignal>(chargeSignals)));
            return data;
        }

        private static TickPresentationData CreateEnemyActionWindupPresentationData(int ownerEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                new[]
                {
                    new TickEnemyActionPresentationSignal(
                        ownerEntityId,
                        EnemyActionKind.Melee,
                        activeActionSequence: 3,
                        startedThisTick: true,
                        canceledThisTick: false,
                        executedThisTick: false,
                        startedRecoveryThisTick: false),
                },
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyUtilityPresentationData(
            int ownerEntityId,
            EnemyUtilityPresentationPhase phase,
            int startTick,
            int executeTick)
        {
            var data = CreateEmptyEnemyAudioPresentationData();
            SetSerializedField(
                typeof(TickPresentationData),
                data,
                "_enemyUtilitySignals",
                new ReadOnlyCollection<TickEnemyUtilityPresentationSignal>(
                    new List<TickEnemyUtilityPresentationSignal>
                    {
                        new TickEnemyUtilityPresentationSignal(
                            ownerEntityId,
                            EnemyUtilityPresentationKind.GravityFieldAura,
                            phase,
                            startTick,
                            executeTick,
                            durationTicks: 2),
                    }));
            return data;
        }

        private static TickPresentationData CreateEnemyGlidePresentationData(
            int ownerEntityId,
            EnemyGlidePhase phase,
            int phaseElapsedTicks,
            bool includeNonMoveMotion = false)
        {
            var data = CreateEmptyEnemyAudioPresentationData();
            var anchorCell = new SurfaceCell(FaceId.Floor, 0, 0);
            if (includeNonMoveMotion)
            {
                SetSerializedField(
                    typeof(TickPresentationData),
                    data,
                    "_entityMotions",
                    new ReadOnlyCollection<TickEntityMotion>(
                        new List<TickEntityMotion>
                        {
                            new TickEntityMotion(
                                ownerEntityId,
                                TickEntityMotionKind.Push,
                                anchorCell,
                                anchorCell),
                        }));
            }

            SetSerializedField(
                typeof(TickPresentationData),
                data,
                "_enemyGlideSignals",
                new ReadOnlyCollection<TickEnemyGlidePresentationSignal>(
                    new List<TickEnemyGlidePresentationSignal>
                    {
                        new TickEnemyGlidePresentationSignal(
                            ownerEntityId,
                            anchorCell,
                            phase,
                            sequence: 1,
                            phaseElapsedTicks: phaseElapsedTicks,
                            phaseTotalTicks: 2,
                            normalizedPhaseProgress: phaseElapsedTicks == 0 ? 0f : 0.5f,
                            liftHeightUnits: 1024,
                            recoveryDipHeightUnits: 0,
                            currentHeightUnits: phase == EnemyGlidePhase.Active ? 1024 : 0,
                            isAirborneVisual: phase == EnemyGlidePhase.Active,
                            wantsRecover: false,
                            isTerminalZero: false),
                    }));
            return data;
        }

        private static TickPresentationData CreateSummonActivePresentationData(
            int spawnedEntityId,
            int sourceEntityId,
            EnemyUnitArchetypeId? archetypeId = null)
        {
            var data = CreateEmptyEnemyAudioPresentationData();
            var spawnCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var resolvedArchetypeId = archetypeId ?? EnemyUnitArchetypeId.None;
            SetSerializedField(
                typeof(TickPresentationData),
                data,
                "_visibilityChanges",
                new ReadOnlyCollection<TickVisibilityChange>(
                    new List<TickVisibilityChange>
                    {
                        new TickVisibilityChange(
                            spawnedEntityId,
                            TickVisibilityChangeKind.Spawn,
                            spawnCell,
                            topology,
                            Direction.Left),
                    }));
            SetSerializedField(
                typeof(TickPresentationData),
                data,
                "_summonedEnemyPresentationBindings",
                sourceEntityId > 0
                    ? new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                        new List<TickSummonedEnemyPresentationBinding>
                        {
                            new TickSummonedEnemyPresentationBinding(
                                spawnedEntityId,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: resolvedArchetypeId,
                                sourceEntityId: sourceEntityId),
                        })
                    : new ReadOnlyCollection<TickSummonedEnemyPresentationBinding>(
                        new List<TickSummonedEnemyPresentationBinding>()));
            return data;
        }

        private static TickPresentationData CreateEmptyEnemyAudioPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickPresentationData CreateCoreSfxPresentationData(int playerEntityId, int exitedEnemyEntityId)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(playerEntityId, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        exitedEnemyEntityId,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit),
                });
        }

        private static TickPresentationData CreateTopologyLockedCoreSfxPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                new TickTopologyMotion(
                    new CubeTopologyState(FaceId.Floor),
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward),
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.ItemConsume,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                });
        }

        private static TickPresentationData CreateAllCoreSfxPresentationData()
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.ItemConsume,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        40,
                        TickEntityExitCause.BoxDestroy,
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Box,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        50,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 1),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit,
                        sourceActorEntityId: 10),
                    new TickEntityExitPresentationSignal(
                        60,
                        TickEntityExitCause.OutOfBounds,
                        new SurfaceCell(FaceId.Floor, 1, 1),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit),
                });
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null,
            int tickIndex = 1,
            IEnumerable<string> eventLog = null,
            string determinismHash = "")
        {
            var result = new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>());
            SetSerializedField(typeof(TickResult), result, "<PresentationData>k__BackingField", presentationData);
            SetSerializedField(typeof(TickResult), result, "<FinalTopology>k__BackingField", new CubeTopologyState(FaceId.Floor));
            SetSerializedField(typeof(TickResult), result, "<DeterminismHash>k__BackingField", determinismHash);
            SetSerializedField(typeof(TickResult), result, "<Trace>k__BackingField", TickTrace.Empty);
            SetSerializedField(typeof(TickResult), result, "<ObjectiveResult>k__BackingField", StageObjectiveTickResult.NoObjective);
            SetSerializedField(
                typeof(TickResult),
                result,
                "_finalEntities",
                new ReadOnlyCollection<EntityState>(new List<EntityState>(finalEntities ?? Array.Empty<EntityState>())));
            SetSerializedField(
                typeof(TickResult),
                result,
                "_eventLog",
                new ReadOnlyCollection<string>(new List<string>(eventLog ?? Array.Empty<string>())));
            return result;
        }

        private static GameplayAudioMapBundle CreateGameplayAudioMapBundle(
            IReadOnlyDictionary<GameplayAudioSemanticId, AudioAttachmentSlot> gameplayAudioAttachmentSlots = null)
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = new List<UnityEngine.Object>();
            var entryType = typeof(GameplayAudioMap).GetNestedType("Entry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null, "GameplayAudioMap.Entry type is required for runtime-authored map setup.");
            var entries = Array.CreateInstance(entryType, GameplayAudioSemanticCatalog.RequiredOneShotV1.Count);

            for (var i = 0; i < GameplayAudioSemanticCatalog.RequiredOneShotV1.Count; i++)
            {
                var semanticId = GameplayAudioSemanticCatalog.RequiredOneShotV1[i];
                var clip = AudioClip.Create($"{semanticId}_Loop", 4410, 1, 44100, false);
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definition.name = semanticId.ToString();
                ConfigureDefinition(definition, clip, loop: true, AudioCategory.Sfx);
                definitions.Add(clip);
                definitions.Add(definition);

                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(
                    typeof(AudioBinding),
                    binding,
                    "attachmentSlot",
                    gameplayAudioAttachmentSlots != null &&
                    gameplayAudioAttachmentSlots.TryGetValue(semanticId, out var attachmentSlot)
                        ? attachmentSlot
                        : default(AudioAttachmentSlot));
                SetSerializedField(typeof(AudioBinding), binding, "policy", null);

                var entry = Activator.CreateInstance(entryType);
                SetSerializedField(entryType, entry, "SemanticId", semanticId);
                SetSerializedField(entryType, entry, "Binding", binding);
                entries.SetValue(entry, i);
            }

            SetSerializedField(typeof(GameplayAudioMap), map, "entries", entries);

            var blockAudioMap = CreateRequiredCueMap<BlockAudioMap, BlockAudioCue>(
                BlockAudioCueCatalog.RequiredOneShotV1,
                definitions);
            var playerLocomotionAudioMap = CreateRequiredCueMap<PlayerLocomotionAudioMap, PlayerLocomotionAudioCue>(
                PlayerLocomotionAudioCueCatalog.RequiredOneShotV1,
                definitions);
            var topologyAudioMap = CreateRequiredCueMap<TopologyAudioMap, TopologyAudioCue>(
                TopologyAudioCueCatalog.RequiredOneShotV1,
                definitions);
            var gravityFieldAudioMap = CreateRequiredCueMap<GravityFieldAudioMap, GravityFieldAudioCue>(
                GravityFieldAudioCueCatalog.RequiredOneShotV1,
                definitions);
            var tileFeatureAudioMap = CreateRequiredCueMap<TileFeatureAudioMap, TileFeatureAudioCue>(
                TileFeatureAudioCueCatalog.RequiredOneShotV1,
                definitions);
            var config = ScriptableObject.CreateInstance<GameplayPresentationAudioConfig>();
            config.name = "GameplayPresentationAudioConfig_PlayModeTest";
            SetSerializedField(typeof(GameplayPresentationAudioConfig), config, "gameplayAudioMap", map);
            SetSerializedField(typeof(GameplayPresentationAudioConfig), config, "blockAudioMap", blockAudioMap);
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "playerLocomotionAudioMap",
                playerLocomotionAudioMap);
            SetSerializedField(typeof(GameplayPresentationAudioConfig), config, "topologyAudioMap", topologyAudioMap);
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "gravityFieldAudioMap",
                gravityFieldAudioMap);
            SetSerializedField(
                typeof(GameplayPresentationAudioConfig),
                config,
                "tileFeatureAudioMap",
                tileFeatureAudioMap);

            definitions.Add(config);
            return new GameplayAudioMapBundle(map, config, definitions.ToArray());
        }

        private static TMap CreateRequiredCueMap<TMap, TCue>(
            IReadOnlyList<TCue> requiredCues,
            ICollection<UnityEngine.Object> trackedObjects)
            where TMap : ScriptableObject
            where TCue : struct
        {
            var map = ScriptableObject.CreateInstance<TMap>();
            map.name = $"{typeof(TMap).Name}_PlayModeTest";
            trackedObjects.Add(map);

            var entryType = typeof(TMap).GetNestedType("Entry", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null, $"{typeof(TMap).Name}.Entry type is required for runtime-authored map setup.");
            var entries = Array.CreateInstance(entryType, requiredCues.Count);

            for (var i = 0; i < requiredCues.Count; i++)
            {
                var cue = requiredCues[i];
                var clip = AudioClip.Create($"{typeof(TCue).Name}_{cue}", 4410, 1, 44100, false);
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                definition.name = $"{typeof(TCue).Name}_{cue}";
                ConfigureDefinition(definition, clip, loop: false, AudioCategory.Sfx);
                trackedObjects.Add(clip);
                trackedObjects.Add(definition);

                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
                SetSerializedField(typeof(AudioBinding), binding, "policy", null);

                var entry = Activator.CreateInstance(entryType);
                SetSerializedField(entryType, entry, "Cue", cue);
                SetSerializedField(entryType, entry, "Binding", binding);
                entries.SetValue(entry, i);
            }

            SetSerializedField(typeof(TMap), map, "entries", entries);
            return map;
        }

        private static GameplayActionAudioProfile CreateActionAudioProfile(ICollection<UnityEngine.Object> trackedObjects)
        {
            var profile = Track(ScriptableObject.CreateInstance<GameplayActionAudioProfile>(), trackedObjects);
            var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>(), trackedObjects);
            var clip = Track(AudioClip.Create("ActionAudio", 4410, 1, 44100, false), trackedObjects);
            definition.name = "ActionAudioPushWindup";
            ConfigureDefinition(definition, clip, loop: false, AudioCategory.Sfx);

            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
            SetSerializedField(typeof(AudioBinding), binding, "policy", null);

            var entries = new[]
            {
                new GameplayActionAudioEntry
                {
                    Action = GameplayActionKind.Push,
                    Moment = GameplayActionAudioMoment.Windup,
                    Binding = binding,
                    IsOptional = false,
                },
                new GameplayActionAudioEntry
                {
                    Action = GameplayActionKind.Flip,
                    Moment = GameplayActionAudioMoment.Windup,
                    Binding = binding,
                    IsOptional = false,
                },
            };
            SetSerializedField(typeof(GameplayActionAudioProfile), profile, "entries", entries);
            return profile;
        }

        private static EnemyAudioProfileBundle CreateEnemyDeathAudioProfile()
        {
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            profile.name = "EnemyAudioProfile_CoreSfxPlayModeDeath_Test";
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var clip = AudioClip.Create("EnemyDeath_PlayMode", 4410, 1, 44100, false);
            definition.name = "EnemyDeath_PlayMode_Def";
            ConfigureDefinition(definition, clip, loop: false, AudioCategory.Sfx);

            var binding = new AudioBinding();
            SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
            SetSerializedField(typeof(AudioBinding), binding, "attachmentSlot", default(AudioAttachmentSlot));
            SetSerializedField(typeof(AudioBinding), binding, "policy", null);

            var entries = new[]
            {
                new EnemyAudioEntry
                {
                    Cue = EnemyAudioCue.Death,
                    Binding = binding,
                    IsOptional = false,
                },
            };
            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            return new EnemyAudioProfileBundle(profile, definition, clip);
        }

        private static EnemyAudioProfileBundle CreateEnemyAudioProfileBundle(params EnemyAudioEntrySpec[] specs)
        {
            var ownedObjects = new List<UnityEngine.Object>();
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            profile.name = "EnemyAudioProfile_PlayMode_Runtime_Test";
            var entries = new EnemyAudioEntry[specs?.Length ?? 0];
            for (var i = 0; i < entries.Length; i++)
            {
                var spec = specs[i];
                var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                var clip = AudioClip.Create($"EnemyAudio_{spec.Cue}", 4410, 1, 44100, false);
                definition.name = $"EnemyAudio_{spec.Cue}_Def";
                ConfigureDefinition(definition, clip, spec.Loop, AudioCategory.Sfx);
                ownedObjects.Add(definition);
                ownedObjects.Add(clip);

                var binding = new AudioBinding();
                SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                SetSerializedField(
                    typeof(AudioBinding),
                    binding,
                    "attachmentSlot",
                    spec.HasAttachmentSlot ? AudioAttachmentSlot.FromId("body") : default(AudioAttachmentSlot));
                SetSerializedField(typeof(AudioBinding), binding, "policy", null);

                entries[i] = new EnemyAudioEntry
                {
                    Cue = spec.Cue,
                    Binding = binding,
                    IsOptional = false,
                };
            }

            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            profile.ValidateOrThrow();
            return new EnemyAudioProfileBundle(profile, ownedObjects.ToArray());
        }

        private static void ConfigureDefinition(
            SingleAudioDefinition definition,
            AudioClip clip,
            bool loop,
            AudioCategory category)
        {
            SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
            SetSerializedField(typeof(AudioDefinition), definition, "loop", loop);
            SetSerializedField(typeof(AudioDefinition), definition, "category", category);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private static object GetInstanceFieldValue(Type declaringType, object target, string fieldName)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            return field.GetValue(target);
        }

        private static EntityState CreatePlayerEntityState()
        {
            return new EntityState
            {
                entityId = 10,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateUnit(int entityId, UnitRole role, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = role == UnitRole.Player ? 1 : 2,
                type = EntityType.Unit,
                unitRole = role,
                state = EntityPhaseState.Idle,
                facing = Direction.Up,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static T Track<T>(T unityObject, ICollection<UnityEngine.Object> trackedObjects)
            where T : UnityEngine.Object
        {
            trackedObjects.Add(unityObject);
            return unityObject;
        }

        private readonly struct EnemyAudioEntrySpec
        {
            public EnemyAudioEntrySpec(
                EnemyAudioCue cue,
                bool loop,
                bool hasAttachmentSlot)
            {
                Cue = cue;
                Loop = loop;
                HasAttachmentSlot = hasAttachmentSlot;
            }

            public EnemyAudioCue Cue { get; }

            public bool Loop { get; }

            public bool HasAttachmentSlot { get; }
        }

        private sealed class HostAudioIntegrationContext : IDisposable
        {
            private readonly GameplayAudioMapBundle _mapBundle;
            private readonly GameObject _rootObject;

            public HostAudioIntegrationContext(
                GameObject rootObject,
                GameplaySceneHost host,
                AudioRuntimeInstaller installer,
                AudioManager manager,
                GameplayAudioMapBundle mapBundle,
                RecordingAudioSettingsPersistenceStore persistenceStore)
            {
                _rootObject = rootObject;
                Host = host;
                Installer = installer;
                Manager = manager;
                _mapBundle = mapBundle;
                PersistenceStore = persistenceStore;
            }

            public GameplaySceneHost Host { get; }

            public AudioRuntimeInstaller Installer { get; }

            public AudioManager Manager { get; }

            public RecordingAudioSettingsPersistenceStore PersistenceStore { get; }

            public void Dispose()
            {
                _mapBundle.Dispose();
                UnityEngine.Object.DestroyImmediate(_rootObject);
            }
        }

        private sealed class RuntimeActionAudioViewFactory : IGameplayEntityViewFactory
        {
            private readonly GameplayActionAudioProfile _profile;

            public RuntimeActionAudioViewFactory(GameplayActionAudioProfile profile)
            {
                _profile = profile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (entity.unitRole == UnitRole.Player)
                {
                    var authoring = viewObject.AddComponent<GameplayActionAudioAuthoring>();
                    SetSerializedField(typeof(GameplayActionAudioAuthoring), authoring, "profile", _profile);
                }

                return view;
            }
        }

        private sealed class RuntimeCoreSfxViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;
            private readonly EnemyAudioProfile _enemyAudioProfile;

            public RuntimeCoreSfxViewFactory(Transform parent, EnemyAudioProfile enemyAudioProfile)
            {
                _parent = parent;
                _enemyAudioProfile = enemyAudioProfile;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (entity.unitRole == UnitRole.Enemy &&
                    _enemyAudioProfile != null)
                {
                    var authoring = viewObject.AddComponent<EnemyAudioAuthoring>();
                    SetSerializedField(typeof(EnemyAudioAuthoring), authoring, "profile", _enemyAudioProfile);
                }

                return view;
            }
        }

        private sealed class GameplayAudioMapBundle : IDisposable
        {
            private readonly UnityEngine.Object[] _ownedObjects;

            public GameplayAudioMapBundle(
                GameplayAudioMap map,
                GameplayPresentationAudioConfig config,
                UnityEngine.Object[] ownedObjects)
            {
                Map = map;
                Config = config;
                _ownedObjects = ownedObjects;
            }

            public GameplayAudioMap Map { get; }

            public GameplayPresentationAudioConfig Config { get; }

            public void Dispose()
            {
                for (var i = 0; i < _ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(Map);
            }
        }

        private sealed class EnemyAudioProfileBundle : IDisposable
        {
            private readonly UnityEngine.Object[] _ownedObjects;

            public EnemyAudioProfileBundle(
                EnemyAudioProfile profile,
                params UnityEngine.Object[] ownedObjects)
            {
                Profile = profile;
                _ownedObjects = ownedObjects;
            }

            public EnemyAudioProfile Profile { get; }

            public void Dispose()
            {
                for (var i = 0; i < _ownedObjects.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
                }

                UnityEngine.Object.DestroyImmediate(Profile);
            }
        }

        private sealed class RecordingAudioSettingsPersistenceStore : IAudioSettingsPersistenceStore
        {
            public int SaveCallCount { get; private set; }

            public AudioSettingsSnapshot Snapshot { get; private set; } = AudioSettingsSnapshot.Default;

            public AudioSettingsSnapshot Load()
            {
                return Snapshot;
            }

            public void Save(AudioSettingsSnapshot snapshot)
            {
                SaveCallCount++;
                Snapshot = snapshot;
            }
        }
    }
}
