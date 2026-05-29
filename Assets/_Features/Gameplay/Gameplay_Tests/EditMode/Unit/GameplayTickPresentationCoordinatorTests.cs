using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTickPresentationCoordinatorTests
    {
        private const string MoonGeneratorPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab";

        private const string MoonGeneratorDoorOpenClipPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Animations/MoonBlockGenerator_DoorOpen.anim";
        private const string EnemyJumpAnimatorControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Jump.controller";
        private const string GravityFieldLockableShaderName = "Game/Presentation/GravityFieldLockableBoxLit";
        private const string GravityFieldLockedWeightProperty = "_GravityFieldLockedWeight";
        private const string GravityFieldLockRevealProperty = "_GravityFieldLockReveal";
        private const string GravityFieldLockNoiseMapProperty = "_GravityFieldLockNoiseMap";
        private const string GravityFieldLockEdgeWidthProperty = "_GravityFieldLockEdgeWidth";
        private const string GravityFieldLockedTintProperty = "_GravityFieldLockedTint";
        private const string GravityFieldDimFactorProperty = "_GravityFieldDimFactor";
        private const string GravityFieldTintStrengthProperty = "_GravityFieldTintStrength";
        private const string GravityFieldEmissionSuppressionProperty = "_GravityFieldEmissionSuppression";
        private const float GravityFieldLockRevealInSeconds = 0.234f;
        private const float GravityFieldLockRevealOutSeconds = 0.208f;
        private const string EnemyInactiveBlendProperty = "_InactiveBlend";
        private const string EnemyInactiveNoiseRevealProperty = "_InactiveNoiseReveal";
        private const string EnemyInactiveTintProperty = "_InactiveTint";
        private const string EnemyInactiveDesaturateStrengthProperty = "_DesaturateStrength";
        private const string EnemyInactiveEmissionSuppressionProperty = "_EmissionSuppression";
        private const float EnemyInactiveRevealInSeconds = 0.25f;
        private const float EnemyInactiveRevealOutSeconds = 0.18f;
        private const string StaticBoxShowcasePrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_Showcase.prefab";
        private const string StaticBoxShowcaseMaterialPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Profiles/M_Static_Box_Showcase.mat";
        private const string GravityFieldLockableMaterialDirectory =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Materials/GravityFieldLockable";
        private static readonly string[] GravityFieldLockableBoxPrefabPaths =
        {
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_Block_Tutorial.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_GravityBlock.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_MetalBlock_.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_MoonBox_Showcase.prefab",
        };

        [Test]
        [Category("Extended")]
        public void CurrentTilePresentationRequests_DefaultsEmpty()
        {
            var coordinator = new GameplayTickPresentationCoordinator();

            Assert.That(coordinator.CurrentTilePresentationRequests, Is.Not.Null);
            Assert.That(coordinator.CurrentTilePresentationRequests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void CurrentGravityFieldPresentationRequests_DefaultsEmpty()
        {
            var coordinator = new GameplayTickPresentationCoordinator();

            Assert.That(coordinator.CurrentGravityFieldPresentationRequests, Is.Not.Null);
            Assert.That(coordinator.CurrentGravityFieldPresentationRequests, Is.Empty);
            Assert.That(coordinator.CurrentGravityFieldVisualStates, Is.Not.Null);
            Assert.That(coordinator.CurrentGravityFieldVisualStates, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_PresentInitial_CallsInitialExtensionOnceAfterViewsExist()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_CallsInitialExtensionOnceAfterViewsExist));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var extension = new RecordingInitialPresentationExtension();
                var player = CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 1));
                var signal = new EntitySpawnPresentationSignal(
                    player.entityId,
                    EntityPresentationKind.Player,
                    EntitySpawnPresentationReason.InitialStageStart,
                    player.position,
                    topology,
                    player.facing,
                    sourceTileFeature: null);
                var initialPresentationData = new InitialPresentationData(new[] { signal });

                presenter.AttachPresentationExtension(extension);
                presenter.PresentInitial(new[] { player }, topology, initialPresentationData);
                presenter.Present(CreateTickResult(1, new[] { player }, topology, TickPresentationData.Empty));

                Assert.That(extension.InitialPresentCallCount, Is.EqualTo(1));
                Assert.That(extension.TickPresentCallCount, Is.EqualTo(1));
                Assert.That(extension.CapturedInitialPresentationData, Is.SameAs(initialPresentationData));
                Assert.That(extension.HadPlayerViewDuringInitial, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_PresentationPause_FreezesExtensionProgressUntilResume()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentationPause_FreezesExtensionProgressUntilResume));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var extension = new RecordingPausablePresentationExtension();
                presenter.AttachPresentationExtension(extension);
                presenter.PresentInitial(Array.Empty<EntityState>(), topology);

                presenter.SetPresentationPaused(true);
                presenter.UpdatePresentation(0.5f);

                Assert.That(presenter.IsPresentationPaused, Is.True);
                Assert.That(extension.IsPaused, Is.True);
                Assert.That(extension.PauseCallCount, Is.EqualTo(1));
                Assert.That(extension.UpdateCallCount, Is.Zero);
                Assert.That(extension.AdvancedSeconds, Is.EqualTo(0f).Within(0.0001f));

                presenter.SetPresentationPaused(false);
                presenter.UpdatePresentation(0.5f);

                Assert.That(presenter.IsPresentationPaused, Is.False);
                Assert.That(extension.IsPaused, Is.False);
                Assert.That(extension.ResumeCallCount, Is.EqualTo(1));
                Assert.That(extension.UpdateCallCount, Is.EqualTo(1));
                Assert.That(extension.AdvancedSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DirectParticleSystem_GameplayPause_PausesAndRestoresPreviousPlayingState()
        {
            var rootObject = new GameObject(nameof(DirectParticleSystem_GameplayPause_PausesAndRestoresPreviousPlayingState));

            try
            {
                var particles = rootObject.AddComponent<ParticleSystem>();
                particles.Play(withChildren: true);
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);

                registry.SetPresentationPaused(true);
                Assert.That(particles.isPaused, Is.True);

                registry.SetPresentationPaused(false);
                Assert.That(particles.isPlaying, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisual_GameplayPause_DoesNotAdvanceOrEmit()
        {
            var rootObject = new GameObject(nameof(TileFeatureVisual_GameplayPause_DoesNotAdvanceOrEmit));

            try
            {
                var target = rootObject.AddComponent<TileFeatureVisualTargetView>();
                var particles = rootObject.AddComponent<ParticleSystem>();
                SetPrivateField(target, "buttonActivatedParticles", particles);
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);
                registry.SetPresentationPaused(true);

                target.PlayButtonActivated();

                Assert.That(particles.isPaused, Is.True);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisual_GameplayPause_DoesNotAdvanceOrEmit()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisual_GameplayPause_DoesNotAdvanceOrEmit));

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();
                var particles = rootObject.AddComponent<ParticleSystem>();
                SetPrivateField(target, "activatedParticles", particles);
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);
                registry.SetPresentationPaused(true);

                target.PlayGravityFieldActivated();

                Assert.That(particles.isPaused, Is.True);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyFloatingPresentation_GameplayPause_DoesNotAdvanceOffset()
        {
            var rootObject = new GameObject(nameof(EnemyFloatingPresentation_GameplayPause_DoesNotAdvanceOffset));
            var targetObject = new GameObject("Target");

            try
            {
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var driver = rootObject.AddComponent<EnemyFloatingPresentationDriver>();
                SetPrivateField(driver, "target", targetObject.transform);
                driver.CaptureBaseLocalPosition();
                driver.SetPresentationPaused(true);

                driver.Advance(1f);

                Assert.That(driver.CurrentOffset, Is.EqualTo(Vector3.zero));
                Assert.That(targetObject.transform.localPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisual_GameplayPause_DoesNotAdvancePulse()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisual_GameplayPause_DoesNotAdvancePulse));

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.SetPresentationPaused(true);

                controller.AdvanceInactiveNoiseReveal(1f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPupilVisual_GameplayPause_DoesNotAdvanceLookMotion()
        {
            var rootObject = new GameObject(nameof(EnemyPupilVisual_GameplayPause_DoesNotAdvanceLookMotion));

            try
            {
                var controller = rootObject.AddComponent<EnemyPupilVisualController>();
                InvokePrivate(controller, "BeginWindup", 1f);
                controller.SetPresentationPaused(true);

                controller.Advance(0.5f);

                Assert.That(GetPrivateField<float>(controller, "_phaseElapsedSeconds"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void AnimatorDrivenGameplayPresentation_GameplayPause_FreezesAndRestoresSpeed()
        {
            var rootObject = new GameObject(nameof(AnimatorDrivenGameplayPresentation_GameplayPause_FreezesAndRestoresSpeed));

            try
            {
                var animator = rootObject.AddComponent<Animator>();
                animator.speed = 0.75f;
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);

                registry.SetPresentationPaused(true);
                Assert.That(animator.speed, Is.EqualTo(0f));

                registry.SetPresentationPaused(false);
                Assert.That(animator.speed, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_NoTileEvents_StaysEmpty()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_NoTileEvents_StaysEmpty));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(1, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Not.Null);
                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_ButtonActivated_ExposesRequest()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_ButtonActivated_ExposesRequest));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));

                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                var request = presenter.CurrentTilePresentationRequests[0];
                Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.ButtonActivated));
                Assert.That(request.TileId, Is.EqualTo(100));
                Assert.That(request.Cell, Is.EqualTo(cell));
                Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
                Assert.That(request.SourceEntityId, Is.EqualTo(101));
                Assert.That(request.OwnerEntityId, Is.EqualTo(102));
                Assert.That(request.TeamId, Is.EqualTo(103));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_PreservesOrderAndDuplicates()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_PreservesOrderAndDuplicates));
            var firstCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 1, 0);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var duplicate = CreateButtonActivatedTileEvent(30, firstCell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(
                        duplicate,
                        CreateButtonActivatedTileEvent(10, secondCell),
                        duplicate)));

                Assert.That(
                    presenter.CurrentTilePresentationRequests.Select(request => request.TileId).ToArray(),
                    Is.EqualTo(new[] { 30, 10, 30 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_ReplacesAndClearsPerTick()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_ReplacesAndClearsPerTick));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(
                        CreateButtonActivatedTileEvent(100, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateButtonActivatedTileEvent(200, new SurfaceCell(FaceId.Floor, 1, 0)))));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(2));

                presenter.Present(CreateTickResult(
                    2,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(300, new SurfaceCell(FaceId.Floor, 2, 0)))));

                Assert.That(
                    presenter.CurrentTilePresentationRequests.Select(request => request.TileId).ToArray(),
                    Is.EqualTo(new[] { 300 }));

                presenter.Present(CreateTickResult(3, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_ReplacesAndClearsPerTick()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_ReplacesAndClearsPerTick));
            var firstCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 1, 0);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, firstCell),
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Expired, 31, secondCell))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(2));
                Assert.That(
                    presenter.CurrentGravityFieldPresentationRequests.Select(request => request.EmitterEntityId).ToArray(),
                    Is.EqualTo(new[] { 30, 31 }));

                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_IsReadOnlyDefensiveCopy()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_IsReadOnlyDefensiveCopy));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Activated,
                        30,
                        new SurfaceCell(FaceId.Floor, 0, 0)))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Is.InstanceOf<ReadOnlyCollection<GravityFieldPresentationRequest>>());
                var list = (IList<GravityFieldPresentationRequest>)presenter.CurrentGravityFieldPresentationRequests;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldVisualStates_ReplacesClearsAndIsReadOnly()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldVisualStates_ReplacesClearsAndIsReadOnly));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var visualState = CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[]
                    {
                        cell,
                    },
                    slotVisibilityMask: 1 << 4);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldVisualPresentationData(visualState)));

                Assert.That(presenter.CurrentGravityFieldVisualStates, Is.InstanceOf<ReadOnlyCollection<GravityFieldVisualState>>());
                Assert.That(presenter.CurrentGravityFieldVisualStates, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].EmitterEntityId, Is.EqualTo(30));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].AreaCells.ToArray(), Is.EqualTo(new[] { cell }));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].AreaFootprint.IsSlotVisible(4), Is.True);
                var list = (IList<GravityFieldVisualState>)presenter.CurrentGravityFieldVisualStates;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));

                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentGravityFieldVisualStates, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_DoNotReplaceRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_DoNotReplaceRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(
                        new[] { CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Charging, timerTicks: 3, durationTicks: 10) },
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, cell))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldVisualStates, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldRequests_InvokeEntityVisualTargets()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldRequests_InvokeEntityVisualTargets));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldPresentationData(
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, cell),
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Expired, 30, cell))));

                Assert.That(target.ActivatedCount, Is.EqualTo(1));
                Assert.That(target.ExpiredCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_InvokeAndClearContinuousTargets()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_InvokeAndClearContinuousTargets));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5))));

                Assert.That(target.ApplyContinuousCount, Is.EqualTo(1));
                Assert.That(target.ClearContinuousCount, Is.Zero);
                Assert.That(target.LastContinuousState.EmitterEntityId, Is.EqualTo(30));
                Assert.That(target.LastContinuousState.Phase, Is.EqualTo(GravityFieldPhase.Active));

                presenter.Present(CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty));

                Assert.That(target.ClearContinuousCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_StateDisappearanceClearsAreaSlots()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_StateDisappearanceClearsAreaSlots));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);
            var slots = CreateAreaSlots(rootObject.transform, slotCount: 9);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<GravityFieldVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "areaCellSlots", slots);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            cell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            new[] { cell },
                            slotVisibilityMask: 1 << 4))));

                Assert.That(slots[4].activeSelf, Is.True);

                presenter.Present(CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty));

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugClearContinuousStateCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualTargetView_AppliesAndClearsAuthoredAreaSlots()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisualTargetView_AppliesAndClearsAuthoredAreaSlots));
            var slots = CreateAreaSlots(rootObject.transform, slotCount: 10);
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "areaCellSlots", slots);

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { cell },
                    slotVisibilityMask: (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8)));

                Assert.That(slots[4].activeSelf, Is.True);
                Assert.That(slots[5].activeSelf, Is.True);
                Assert.That(slots[7].activeSelf, Is.True);
                Assert.That(slots[8].activeSelf, Is.True);
                Assert.That(slots[0].activeSelf, Is.False);
                Assert.That(slots[9].activeSelf, Is.False);
                Assert.That(target.DebugLastAreaCellCount, Is.EqualTo(1));
                Assert.That(target.DebugVisibleAreaSlotCount, Is.EqualTo(4));

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Charging,
                    timerTicks: 3,
                    durationTicks: 10,
                    new[] { cell },
                    slotVisibilityMask: 1 << 4));

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { cell },
                    slotVisibilityMask: 1 << 4));
                target.ClearGravityFieldVisualState();

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugLastAreaCellCount, Is.Zero);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualTargetView_MissingAreaSlotsNoOps()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisualTargetView_MissingAreaSlotsNoOps));

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();

                Assert.DoesNotThrow(() => target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { new SurfaceCell(FaceId.Floor, 1, 1) },
                    slotVisibilityMask: 1 << 4)));
                Assert.DoesNotThrow(target.ClearGravityFieldVisualState);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_TracksSourcesAndClearsOnDisable()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_TracksSourcesAndClearsOnDisable));
            var lockedRoot = new GameObject("LockedRoot");
            lockedRoot.SetActive(false);
            lockedRoot.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "lockedVisualRoot", lockedRoot);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ApplyGravityFieldLockedTarget(31);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));

                target.ClearGravityFieldLockedTarget(999);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ClearGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ClearGravityFieldLockedTarget(31);
                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds + 0.01f);
                Assert.That(lockedRoot.activeSelf, Is.False);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                typeof(GravityFieldLockedTargetVisualTargetView)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, Array.Empty<object>());
                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockWeight()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockWeight));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockEdgeWidthProperty), Is.EqualTo(0.08f).Within(0.0001f));
                AssertColorApproximately(
                    new Color(0.45f, 0.55f, 0.85f, 1f),
                    GetRendererColor(renderer, GravityFieldLockedTintProperty));
                Assert.That(GetRendererFloat(renderer, GravityFieldDimFactorProperty), Is.EqualTo(0.55f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldTintStrengthProperty), Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldEmissionSuppressionProperty), Is.EqualTo(0.85f).Within(0.0001f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds * 0.5f + 0.01f);

                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockEmissionSuppression()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockEmissionSuppression));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });
                PlayerViewPrefabTestUtility.SetSerializedField(target, "gravityFieldEmissionSuppression", 0.42f);

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(
                    GetRendererFloat(renderer, GravityFieldEmissionSuppressionProperty),
                    Is.EqualTo(0.42f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_KeepsDimmingUntilLastEmitterClears()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_KeepsDimmingUntilLastEmitterClears));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);
                target.ApplyGravityFieldLockedTarget(2);
                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds + 0.01f);
                target.ClearGravityFieldLockedTarget(1);

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));

                target.ClearGravityFieldLockedTarget(2);

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(1f).Within(0.0001f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds * 0.5f + 0.01f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_OnDisableResetsRendererWeight()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_OnDisableResetsRendererWeight));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);
                typeof(GravityFieldLockedTargetVisualTargetView)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, Array.Empty<object>());

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_NullAndEmptyRenderersNoOp()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_NullAndEmptyRenderersNoOp));

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();

                Assert.DoesNotThrow(() => target.ApplyGravityFieldLockedTarget(1));
                Assert.DoesNotThrow(() => target.ClearGravityFieldLockedTarget(1));

                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", Array.Empty<Renderer>());

                Assert.DoesNotThrow(() => target.ApplyGravityFieldLockedTarget(2));
                Assert.DoesNotThrow(() => target.ClearGravityFieldLockedTarget(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_PreservesExistingPropertyBlockValues()
        {
            const string existingPropertyName = "_ExistingMpbValue";
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_PreservesExistingPropertyBlockValues));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var existingBlock = new MaterialPropertyBlock();
                existingBlock.SetFloat(existingPropertyName, 0.75f);
                renderer.SetPropertyBlock(existingBlock);

                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(GetRendererFloat(renderer, existingPropertyName), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming()
        {
            var lockableShader = Shader.Find(GravityFieldLockableShaderName);

            Assert.That(lockableShader, Is.Not.Null, GravityFieldLockableShaderName);
            foreach (var prefabPath in GravityFieldLockableBoxPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                Assert.That(prefab, Is.Not.Null, prefabPath);
                var target = prefab.GetComponent<GravityFieldLockedTargetVisualTargetView>();
                Assert.That(target, Is.Not.Null, $"{prefabPath} root component");

                var dimRenderers = ReadDimRenderers(target);
                Assert.That(dimRenderers, Is.Not.Null, prefabPath);
                Assert.That(dimRenderers, Is.Not.Empty, prefabPath);
                for (var i = 0; i < dimRenderers.Length; i++)
                {
                    var dimRenderer = dimRenderers[i];

                    Assert.That(dimRenderer, Is.Not.Null, $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.transform.IsChildOf(prefab.transform), Is.True, $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.name, Does.Not.Contain("GripPoint"), $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.GetComponent<ParticleSystem>(), Is.Null, $"{prefabPath} dimRenderers[{i}]");
                    foreach (var material in dimRenderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, $"{prefabPath} {dimRenderer.name}");
                        Assert.That(material.shader, Is.SameAs(lockableShader), $"{prefabPath} {dimRenderer.name} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockRevealProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockNoiseMapProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockEdgeWidthProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldEmissionSuppressionProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.GetTexture(GravityFieldLockNoiseMapProperty), Is.Not.Null, $"{prefabPath} {material.name}");
                        Assert.That(
                            material.GetFloat(GravityFieldLockEdgeWidthProperty),
                            Is.InRange(0.001f, 0.5f),
                            $"{prefabPath} {material.name}");
                    }
                }
            }

            var materialDirectory = Path.Combine(
                Application.dataPath,
                GravityFieldLockableMaterialDirectory.Substring("Assets/".Length));
            var materialPaths = Directory.GetFiles(
                    materialDirectory,
                    "*.mat",
                    SearchOption.TopDirectoryOnly)
                .Select(path => "Assets" + path.Replace("\\", "/").Substring(Application.dataPath.Length))
                .OrderBy(path => path)
                .ToArray();
            Assert.That(materialPaths, Has.Length.EqualTo(18));
            foreach (var materialPath in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                Assert.That(material, Is.Not.Null, materialPath);
                Assert.That(material.shader, Is.SameAs(lockableShader), materialPath);
                Assert.That(material.HasProperty(GravityFieldEmissionSuppressionProperty), Is.True, materialPath);
                Assert.That(material.GetTexture(GravityFieldLockNoiseMapProperty), Is.Not.Null, materialPath);
                Assert.That(material.GetFloat(GravityFieldLockEdgeWidthProperty), Is.InRange(0.001f, 0.5f), materialPath);
            }
        }

        [Test]
        [Category("Core")]
        public void StaticViewBoxShowcase_RemainsExcludedFromGravityFieldLockableDimming()
        {
            var showcasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaticBoxShowcasePrefabPath);
            var showcaseMaterial = AssetDatabase.LoadAssetAtPath<Material>(StaticBoxShowcaseMaterialPath);

            Assert.That(showcasePrefab, Is.Not.Null, StaticBoxShowcasePrefabPath);
            Assert.That(showcasePrefab.GetComponent<GravityFieldLockedTargetVisualTargetView>(), Is.Null);
            Assert.That(showcaseMaterial, Is.Not.Null, StaticBoxShowcaseMaterialPath);
            Assert.That(showcaseMaterial.shader.name, Is.Not.EqualTo(GravityFieldLockableShaderName));
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_MissingUnsupportedAndDuplicate_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_MissingUnsupportedAndDuplicate_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter }, topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(999, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5),
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5))));

                Assert.That(diagnostics.Any(message => message.Contains("missing continuous visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported continuous visual target")), Is.True);

                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 4, durationTicks: 5),
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Charging, timerTicks: 9, durationTicks: 10))));

                Assert.That(target.ApplyContinuousCount, Is.EqualTo(1));
                Assert.That(target.LastContinuousState.Phase, Is.EqualTo(GravityFieldPhase.Active));
                Assert.That(target.LastContinuousState.TimerTicks, Is.EqualTo(4));
                Assert.That(diagnostics.Any(message => message.Contains("duplicate continuous visual state")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_ApplyRetainLateAttachAndClear()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_ApplyRetainLateAttachAndClear));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked target visual target")), Is.True);

                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
                Assert.That(target.LastLockedTargetEmitterEntityId, Is.EqualTo(30));

                presenter.Present(CreateTickResult(
                    3,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(2));

                presenter.Present(CreateTickResult(4, new[] { emitter, targetBox }, topology, TickPresentationData.Empty));

                Assert.That(target.ClearLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.LastLockedTargetEmitterEntityId, Is.EqualTo(30));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MissingAndUnsupported_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MissingAndUnsupported_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20, 999 }))));

                Assert.That(diagnostics.Any(message => message.Contains("missing locked target visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked target visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MultipleEmittersMaintainSourceSet()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MultipleEmittersMaintainSourceSet));
            var firstEmitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var secondEmitterCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var firstEmitter = CreateBox(30, firstEmitterCell);
            var secondEmitter = CreateBox(31, secondEmitterCell);
            var targetBox = CreateBox(20, targetCell);
            var lockedRoot = new GameObject("LockedRoot");
            lockedRoot.SetActive(false);
            lockedRoot.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { firstEmitter, secondEmitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "lockedVisualRoot", lockedRoot);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { firstEmitter, secondEmitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            firstEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }),
                        CreateGravityFieldVisualState(
                            31,
                            secondEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.Present(CreateTickResult(
                    2,
                    new[] { firstEmitter, secondEmitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            31,
                            secondEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.Present(CreateTickResult(3, new[] { firstEmitter, secondEmitter, targetBox }, topology, TickPresentationData.Empty));

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.UpdatePresentation(GravityFieldLockRevealOutSeconds * 0.5f);
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.UpdatePresentation(GravityFieldLockRevealOutSeconds * 0.5f + 0.01f);
                Assert.That(lockedRoot.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_DuplicateTargetIdsApplyOnce()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_DuplicateTargetIdsApplyOnce));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20, 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_CallsTargetInterfaceAndDoesNotClearDimming()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_CallsTargetInterfaceAndDoesNotClearDimming));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);
            var payload = new GravityFieldLockedBoxPayload(30, 20, emitterCell, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(
                        new[]
                        {
                            CreateGravityFieldVisualState(
                                30,
                                emitterCell,
                                GravityFieldPhase.Active,
                                timerTicks: 2,
                                durationTicks: 5,
                                lockedTargetEntityIds: new[] { 20 }),
                        },
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.LockedBox,
                            30,
                            emitterCell,
                            targetEntityId: 20,
                            lockedBoxPayload: payload))));

                Assert.That(target.LockedBoxOneShotCount, Is.EqualTo(1));
                Assert.That(target.LastLockedBoxPayload, Is.EqualTo(payload));
                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_MissingUnsupportedAndLateAttachDoNotReplay()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_MissingUnsupportedAndLateAttachDoNotReplay));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);
            var payload = new GravityFieldLockedBoxPayload(30, 20, emitterCell, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.LockedBox,
                        30,
                        emitterCell,
                        targetEntityId: 20,
                        lockedBoxPayload: payload))));

                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();
                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                presenter.Present(CreateTickResult(
                    3,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.LockedBox,
                        30,
                        emitterCell,
                        targetEntityId: 999,
                        lockedBoxPayload: new GravityFieldLockedBoxPayload(30, 999, emitterCell, targetCell)))));

                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked box one-shot visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("missing locked box one-shot visual target")), Is.True);
                Assert.That(target.LockedBoxOneShotCount, Is.Zero);
                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_DoesNotCreateSnapshots()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_DoesNotCreateSnapshots));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    presenter.Present(CreateTickResult(
                        1,
                        new[] { emitter, targetBox },
                        topology,
                        CreateGravityFieldVisualPresentationData(
                            CreateGravityFieldVisualState(
                                30,
                                emitterCell,
                                GravityFieldPhase.Active,
                                timerTicks: 2,
                                durationTicks: 5,
                                lockedTargetEntityIds: new[] { 20 }))));

                    Assert.That(capture.Counts.WorldStateCreateSnapshotCount, Is.Zero);
                    Assert.That(capture.Counts.ProjectedWorldMaterializedSnapshotCount, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MissingOnClearNoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MissingOnClearNoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                UnityEngine.Object.DestroyImmediate(targetView.gameObject);
                registry.Unregister(20);

                Assert.DoesNotThrow(() => presenter.Present(
                    CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty)));
                Assert.That(diagnostics.Any(message => message.Contains("missing locked target clear visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualMissingOrUnsupported_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualMissingOrUnsupported_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitter = CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Activated,
                        999,
                        new SurfaceCell(FaceId.Floor, 0, 0)))));

                presenter.PresentInitial(new[] { emitter }, topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Expired,
                        30,
                        emitter.position))));

                Assert.That(diagnostics.Any(message => message.Contains("missing Activated visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported Expired visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_IsReadOnlyDefensiveCopy()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_IsReadOnlyDefensiveCopy));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, new SurfaceCell(FaceId.Floor, 0, 0)))));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.InstanceOf<ReadOnlyCollection<TilePresentationRequest>>());
                var list = (IList<TilePresentationRequest>)presenter.CurrentTilePresentationRequests;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_ButtonActivatedRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_ButtonActivatedRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));

                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TileId, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_TopologyMotion_SyncsTileFeaturePoseBeforePlayingRequests()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_TopologyMotion_SyncsTileFeaturePoseBeforePlayingRequests));
            var targetObject = new GameObject("DestinationTileFeatureTarget");
            var destinationCell = new SurfaceCell(FaceId.Ceiling, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var sourceTopology);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var target = targetObject.AddComponent<ActiveStateRecordingTileFeatureTarget>();
                target.Configure(100, destinationCell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var poseResolver = new BoardSurfaceCellPresentationPoseResolver(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f,
                    sourceTopology,
                    1f);
                var synchronizer = new TileFeatureVisualPoseSynchronizer(registry, poseResolver);
                synchronizer.RefreshAll(sourceTopology);
                presenter.AttachTileFeatureVisualRegistry(registry);
                presenter.AttachTileFeatureVisualPoseSynchronizer(synchronizer);

                Assert.That(targetObject.activeSelf, Is.False);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    destinationTopology,
                    CreateTilePresentationDataWithTopologyMotion(
                        new TickTopologyMotion(sourceTopology, destinationTopology, CubeRotationKind.Forward),
                        CreateButtonActivatedTileEvent(100, destinationCell))));

                Assert.That(target.PlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(target.WasActiveInHierarchyWhenButtonActivated, Is.True);
                Assert.That(targetObject.activeSelf, Is.True);
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TileId, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_DestroyTileRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_DestroyTileRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateDestroyTileTriggeredTileEvent(100, cell, targetEntityId: 20))));

                Assert.That(target.DebugPlayDestroyTileTriggeredCount, Is.EqualTo(1));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.DestroyTileTriggered));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TargetEntityId, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockGeneratedRequest_InvokesGeneratorTileVisualTarget()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockGeneratedRequest_InvokesGeneratorTileVisualTarget));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(target.DebugPlayMoonBlockGeneratedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastMoonBlockGeneratedEntityId, Is.EqualTo(20));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.MoonBlockGenerated));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TargetEntityId, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorPrefab_UsesAnimatorDoorOpenVisualAndNoDoorDriver()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonGeneratorPrefabPath);

            Assert.That(prefab, Is.Not.Null);

            var target = prefab.GetComponent<TileFeatureVisualTargetView>();
            Assert.That(target, Is.Not.Null);
            Assert.That(target.DebugAnimator, Is.Not.Null);
            Assert.That(target.DebugAnimator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(target.DebugAnimator.runtimeAnimatorController.name, Is.EqualTo("TileFeature_MoonGenerator_Default"));

            var monoBehaviours = prefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            Assert.That(
                monoBehaviours.Select(component => component != null ? component.GetType().Name : string.Empty),
                Does.Not.Contain("MoonBlockGeneratorDoorPresentationDriver"));

            var controller = target.DebugAnimator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "MoonBlockGenerated" &&
                parameter.type == AnimatorControllerParameterType.Trigger), Is.True);
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorDoorOpenClip_TargetsLeftAndRightDoorTransforms()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MoonGeneratorDoorOpenClipPath);

            Assert.That(clip, Is.Not.Null);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Any(binding =>
                binding.path == "ModelRoot/MoonSpawner/L_Door" &&
                binding.propertyName == "localEulerAnglesRaw.z"), Is.True);
            Assert.That(bindings.Any(binding =>
                binding.path == "ModelRoot/MoonSpawner/R_Door" &&
                binding.propertyName == "localEulerAnglesRaw.z"), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockGeneratedBeforeEntityBind_StartsEmergenceWhenViewRegisters()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockGeneratedBeforeEntityBind_StartsEmergenceWhenViewRegisters));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(presenter.PendingMoonBlockEmergenceRequestCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out _), Is.False);

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateBox(20, cell) },
                    topology,
                    TickPresentationData.Empty));

                Assert.That(presenter.PendingMoonBlockEmergenceRequestCount, Is.Zero);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(driver.DebugPlayCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_OnlyMovesModelRootAndNormalizes()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_OnlyMovesModelRootAndNormalizes));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                var expectedRootPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    cell,
                    EntityType.Box);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var rootPositionDuringEmergence = view.transform.localPosition;
                var rootRotationDuringEmergence = view.transform.localRotation;
                var modelRoot = view.ModelRoot;
                AssertPositionApproximately(rootPositionDuringEmergence, expectedRootPosition);
                Assert.That(modelRoot.localPosition.z, Is.LessThan(-0.001f));
                Assert.That(modelRoot.localPosition.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(modelRoot.localPosition.y, Is.EqualTo(0f).Within(0.001f));
                AssertPositionApproximately(modelRoot.localScale, Vector3.zero);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.UpdatePresentation(driver.DebugDurationSeconds + 0.01f);

                AssertPositionApproximately(view.transform.localPosition, rootPositionDuringEmergence);
                Assert.That(view.transform.localRotation, Is.EqualTo(rootRotationDuringEmergence));
                AssertPositionApproximately(modelRoot.localPosition, Vector3.zero);
                AssertPositionApproximately(modelRoot.localScale, Vector3.one);
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_HidesThenLaunchesAfterDoorOpenLead()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_HidesThenLaunchesAfterDoorOpenLead));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var startPosition = view.ModelRoot.localPosition;
                var startScale = view.ModelRoot.localScale;

                presenter.UpdatePresentation(0.05f);

                AssertPositionApproximately(view.ModelRoot.localPosition, startPosition);
                AssertPositionApproximately(startScale, Vector3.zero);
                AssertPositionApproximately(view.ModelRoot.localScale, Vector3.zero);

                presenter.UpdatePresentation(0.04f);

                Assert.That(view.ModelRoot.localPosition.z, Is.GreaterThan(startPosition.z));
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(startScale.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_UsesPresentationLaunchAfterSpawnLockWindow()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_UsesPresentationLaunchAfterSpawnLockWindow));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.IsPlaying, Is.True);

                var lockDurationSeconds =
                    MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks /
                    (float)timingProfile.SimulationTicksPerSecond;
                Assert.That(lockDurationSeconds, Is.LessThan(timingProfile.MoonBlockEmergenceDurationSeconds));
                Assert.That(driver.DebugDurationSeconds, Is.GreaterThan(lockDurationSeconds));

                presenter.UpdatePresentation(lockDurationSeconds + 0.06f);

                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(view.ModelRoot.localPosition.z, Is.GreaterThan(0f));
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));

                presenter.UpdatePresentation(driver.DebugDurationSeconds);

                Assert.That(driver.IsPlaying, Is.False);
                AssertPositionApproximately(view.ModelRoot.localPosition, Vector3.zero);
                AssertPositionApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockEmergencePresentationController_DisposeUnsubscribesAndClearsPending()
        {
            var rootObject = new GameObject(nameof(MoonBlockEmergencePresentationController_DisposeUnsubscribesAndClearsPending));

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var controller = new MoonBlockEmergencePresentationController();
                controller.Configure(registry, CreateTimingProfile());
                controller.QueueRequests(
                    new[]
                    {
                        CreateMoonBlockGeneratedRequest(moonBlockEntityId: 20, spawnTick: 1),
                    },
                    currentTickIndex: 1);
                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));

                controller.Dispose();
                var viewObject = new GameObject("EntityView_20");
                viewObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                registry.Register(view);

                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(view.GetComponent<MoonBlockEmergencePresentationDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockEmergencePresentationController_PendingRequestExpiresIfViewNeverBinds()
        {
            var rootObject = new GameObject(nameof(MoonBlockEmergencePresentationController_PendingRequestExpiresIfViewNeverBinds));

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var controller = new MoonBlockEmergencePresentationController();
                controller.Configure(registry, CreateTimingProfile());
                controller.QueueRequests(
                    new[]
                    {
                        CreateMoonBlockGeneratedRequest(moonBlockEntityId: 20, spawnTick: 1),
                    },
                    currentTickIndex: 1);

                controller.QueueRequests(Array.Empty<TilePresentationRequest>(), currentTickIndex: 8);

                Assert.That(controller.PendingRequestCount, Is.Zero);
                controller.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_ButtonActivatedRequest_NextTickWithoutRequest_DoesNotInvokeAgain()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_ButtonActivatedRequest_NextTickWithoutRequest_DoesNotInvokeAgain));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));
                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_GenericMovePresentation_Retained()
        {
            GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration();
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicTrack_AppliesTickOneDestinationPoseImmediately()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicTrack_AppliesTickOneDestinationPoseImmediately");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var kinematicTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_ComposesWithActiveMove()
        {
            var rootObject = new GameObject("GlidePresentation_ComposesWithActiveMove");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, destinationCell),
                        },
                        topology,
                        CreateGlidePresentationData(
                            new[]
                            {
                                new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, destinationCell),
                            },
                            new[]
                            {
                                CreateGlideSignal(20, destinationCell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit) -
                               GetProjectedEntityNormal(boardBounds, topology, destinationCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_ComposesWithFutureKinematicTrack()
        {
            var rootObject = new GameObject("GlidePresentation_ComposesWithFutureKinematicTrack");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                var kinematicTrack = CreateKinematicTrack(
                    20,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, sourceCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack },
                            enemyGlideSignals: new[]
                            {
                                CreateGlideSignal(20, sourceCell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0) -
                               GetProjectedEntityNormal(boardBounds, topology, sourceCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_RisesAwayFromProjectedNormal_OnNonTopFaceAndClearsWhenSignalMissing()
        {
            var rootObject = new GameObject("GlidePresentation_RisesAwayFromProjectedNormal_OnNonTopFaceAndClearsWhenSignalMissing");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var cell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, cell),
                    },
                    topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, cell),
                        },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, cell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var basePosition = GetProjectedEntityPosition(boardBounds, topology, cell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, cell, EntityType.Unit);
                Assert.That(Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)), Is.LessThan(0.01f));
                AssertPositionApproximately(view.transform.localPosition, basePosition - (normal * 0.25f));

                presenter.Present(
                    CreateTickResult(
                        2,
                        new[]
                        {
                            CreateEnemyUnit(20, cell),
                        },
                        topology,
                        TickPresentationData.Empty));

                AssertPositionApproximately(view.transform.localPosition, basePosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Windup_RaisesInPlaceAndKeepsFacing()
        {
            var rootObject = new GameObject("GliderVisual_Windup_RaisesInPlaceAndKeepsFacing");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, cell) }, topology);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var basePosition = GetProjectedEntityPosition(boardBounds, topology, cell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, cell, EntityType.Unit);
                var initialRotation = view.transform.localRotation;

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, cell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, cell, EnemyGlidePhase.Windup, KinematicFixed.UnitsPerCell / 8),
                            })));

                AssertPositionApproximately(view.transform.localPosition, basePosition - (normal * 0.125f));
                Assert.That(Quaternion.Angle(view.transform.localRotation, initialRotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Active_OnSolidCell_UsesAirborneHeight()
        {
            var rootObject = new GameObject("GliderVisual_Active_OnSolidCell_UsesAirborneHeight");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var solidCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, solidCell) },
                        topology,
                        CreateGlidePresentationData(
                            new[]
                            {
                                new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, solidCell),
                            },
                            new[]
                            {
                                CreateGlideSignal(20, solidCell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));
                presenter.UpdatePresentation(CreateTimingProfile().MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedEntityPosition(boardBounds, topology, solidCell, EntityType.Unit) -
                               GetProjectedEntityNormal(boardBounds, topology, solidCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Active_StepByStepMovementDoesNotSkip()
        {
            var rootObject = new GameObject("GliderVisual_Active_StepByStepMovementDoesNotSkip");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var nextCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                var kinematicTrack = CreateKinematicTrack(
                    20,
                    sourceCell,
                    sourceLocalX: 0,
                    destinationAnchorCell: sourceCell,
                    destinationLocalX: KinematicFixed.UnitsPerCell / 2,
                    topology: topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack },
                            enemyGlideSignals: new[]
                            {
                                CreateGlideSignal(20, sourceCell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedKinematicEntityPosition(
                                   boardBounds,
                                   topology,
                                   sourceCell,
                                   EntityType.Unit,
                                   KinematicFixed.UnitsPerCell / 2,
                                   0) -
                               GetProjectedEntityNormal(boardBounds, topology, sourceCell, EntityType.Unit) * 0.25f;
                var skippedPosition = GetProjectedEntityPosition(boardBounds, topology, nextCell, EntityType.Unit) -
                                      GetProjectedEntityNormal(boardBounds, topology, nextCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
                Assert.That(Vector3.Distance(view.transform.localPosition, skippedPosition), Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Recover_LowersInPlaceOnlyOnNonSolid()
        {
            var rootObject = new GameObject("GliderVisual_Recover_LowersInPlaceOnlyOnNonSolid");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var nonSolidCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, nonSolidCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, nonSolidCell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, nonSolidCell, EnemyGlidePhase.Active, KinematicFixed.UnitsPerCell / 4),
                            })));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var activePose = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        2,
                        new[] { CreateEnemyUnit(20, nonSolidCell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, nonSolidCell, EnemyGlidePhase.Recovery, KinematicFixed.UnitsPerCell / 8),
                            })));

                var basePosition = GetProjectedEntityPosition(boardBounds, topology, nonSolidCell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, nonSolidCell, EntityType.Unit);
                var recoverPose = view.transform.localPosition;
                AssertPositionApproximately(recoverPose, basePosition - (normal * 0.125f));
                Assert.That(Vector3.Distance(recoverPose, basePosition), Is.LessThan(Vector3.Distance(activePose, basePosition)));

                presenter.Present(CreateTickResult(3, new[] { CreateEnemyUnit(20, nonSolidCell) }, topology, TickPresentationData.Empty));

                AssertPositionApproximately(view.transform.localPosition, basePosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicTrack_BeatsLegacyMotionOnAnchorCommit()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicTrack_BeatsLegacyMotionOnAnchorCommit");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var legacyMotion = new TickEntityMotion(
                    10,
                    TickEntityMotionKind.Move,
                    sourceCell,
                    destinationCell,
                    topology,
                    topology,
                    Direction.Right,
                    Direction.Right);
                var kinematicTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 1024,
                    destinationCell,
                    destinationLocalX: -2048,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreatePlayerUnit(10, destinationCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            new[] { legacyMotion },
                            new[] { kinematicTrack })));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds * 2f);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit, -2048, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicRemovedTerminal_RetainsPoseWithoutFinalEntity()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicRemovedTerminal_RetainsPoseWithoutFinalEntity");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    topology,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, -2048, 0));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousPose_AppliesAnchorPlusLocalOffset()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousPose_AppliesAnchorPlusLocalOffset");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var continuousTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    ContinuousLocomotionMode.Moving);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { continuousTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousIdleNonZero_DoesNotSnapToAnchor()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousIdleNonZero_DoesNotSnapToAnchor");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var expectedPosition = GetProjectedKinematicEntityPosition(
                    boardBounds,
                    topology,
                    sourceCell,
                    EntityType.Unit,
                    1024,
                    0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var idleTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 1024,
                    sourceCell,
                    destinationLocalX: 1024,
                    ContinuousLocomotionMode.Idle);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreatePlayerUnit(10, sourceCell) },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { idleTrack })));
                presenter.UpdatePresentation(CreateTimingProfile().MoveMotionDurationSeconds);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreatePlayerUnit(10, sourceCell) },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { idleTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
                Assert.That(
                    Vector3.Distance(
                        view.transform.localPosition,
                        GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit)),
                    Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousRemovedTerminal_RetainsPose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousRemovedTerminal_RetainsPose");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    ContinuousLocomotionMode.Idle,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, -2048, 0));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerDeathHold_RetainsRemovedTerminalPoseUntilSignalClears()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerDeathHold_RetainsRemovedTerminalPoseUntilSignalClears");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var expectedPosition = GetProjectedKinematicEntityPosition(
                    boardBounds,
                    topology,
                    sourceCell,
                    EntityType.Unit,
                    -2048,
                    0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    topology,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack },
                            new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 2,
                                    eligibleTick: 5,
                                    remainingTicks: 3,
                                    startedThisTick: true),
                            })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            Array.Empty<TickKinematicMotionTrack>(),
                            new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 2,
                                    eligibleTick: 5,
                                    remainingTicks: 2,
                                    startedThisTick: false),
                            })));

                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_PlayerMoveMotionOverride_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                const float playerMoveOverrideSeconds = 0.4f;
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var motionAuthoring = playerViewPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();
                Assert.That(motionAuthoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "moveMotionDurationSeconds",
                    playerMoveOverrideSeconds);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreatePlayerUnit(10, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: playerMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float unitMoveOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        unitMoveOverridesByEntityId: new Dictionary<int, float>
                        {
                            { 20, unitMoveOverrideSeconds },
                        },
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 20, new EntityMotionPresentationSnapshot(0.8f, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: unitMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float entityMoveOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 20, new EntityMotionPresentationSnapshot(entityMoveOverrideSeconds, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: entityMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float pushOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 30, new EntityMotionPresentationSnapshot(EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, pushOverrideSeconds, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Push));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Box);
                var expectedPosition = ResolveEasedLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.PushMotionDurationSeconds,
                    durationSeconds: pushOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxFlipMotion_ScalesModelRootDuringRiseImpactAndReset()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxFlipMotion_ScalesModelRootDuringRiseImpactAndReset");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Flip));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.25f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.y, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.GreaterThan(1f));

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.65f);

                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.y, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.10f);

                AssertScaleApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxSlideMotion_StretchesAlongTravelAndResets()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxSlideMotion_StretchesAlongTravelAndResets");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.BoxSlide));
                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(Mathf.Max(view.ModelRoot.localScale.x, view.ModelRoot.localScale.y), Is.GreaterThan(1f));
                Assert.That(Mathf.Min(view.ModelRoot.localScale.x, view.ModelRoot.localScale.y), Is.LessThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                AssertScaleApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_EnemyPrefabUnitMoveOverride_PrefersUnitLocomotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyPrefabUnitMoveOverride_PrefersUnitLocomotionAuthoring");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_UnitMoveOverride",
                unitMoveDurationSeconds: 0.4f,
                entityMoveDurationSeconds: 0.8f);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                        {
                            { 20, enemyPrefab },
                        }));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: 0.4f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_EnemyPrefabUnitMoveAuthoringWithoutOverride_FallsBackToEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyPrefabUnitMoveAuthoringWithoutOverride_FallsBackToEntityMotionAuthoring");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_UnitMoveFallback",
                unitMoveDurationSeconds: UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                entityMoveDurationSeconds: 0.4f);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                        {
                            { 20, enemyPrefab },
                        }));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: 0.4f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_InterpolatesAndKeepsEntityMotionPhase()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_InterpolatesAndKeepsEntityMotionPhase");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    sequence: 1,
                                    phase: EnemyJumpPhase.Airborne,
                                    startedWindupThisTick: false,
                                    startedAirborneThisTick: true,
                                    landedThisTick: false,
                                    retryThisTick: false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var landingPosition = GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit);

                Assert.That(view.transform.localPosition.x, Is.GreaterThan(sourcePosition.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(landingPosition.x - 0.001f));
                Assert.That(view.transform.localPosition, Is.Not.EqualTo(sourcePosition));
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_UsesJumpMotionPresentationEase()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_UsesJumpMotionPresentationEase));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var jumpMotionSnapshot = new EnemyJumpMotionPresentationSnapshot(
                    horizontalHoldBias: 0.12f,
                    apexHoldPower: 4f);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        jumpMotionOverridesByEntityId: new Dictionary<int, EnemyJumpMotionPresentationSnapshot>
                        {
                            { 20, jumpMotionSnapshot },
                        }));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(10, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 10, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 60));

                presenter.UpdatePresentation(0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var landingPosition = GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit);
                var defaultMidpointX = Mathf.Lerp(sourcePosition.x, landingPosition.x, 0.5f);
                var easedHangX = Mathf.Lerp(sourcePosition.x, landingPosition.x, 0.38f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(defaultMidpointX - 0.001f));
                Assert.That(view.transform.localPosition.x, Is.EqualTo(easedHangX).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalBottomFaceVisible()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalBottomFaceVisible));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.JumpDetachedVisibilityStates.TryGetValue(20, out var detachedState), Is.True);
                Assert.That(detachedState.AuthoritativeCell, Is.EqualTo(sourceCell));
                Assert.That(detachedState.AuthoritativeFace, Is.EqualTo(FaceId.Floor));
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsJumpDetachedVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalFrontBackFaceVisible()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalFrontBackFaceVisible));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Ceiling);
                var sourceCell = new SurfaceCell(FaceId.Back, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Back, 2, 0);

                Assert.That(topology.FrontFace, Is.EqualTo(FaceId.Back));
                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.JumpDetachedVisibilityStates.TryGetValue(20, out var detachedState), Is.True);
                Assert.That(detachedState.AuthoritativeFace, Is.EqualTo(FaceId.Back));
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsJumpDetachedVisible, Is.True);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_InactiveFaceDoesNotVisibleOr()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_InactiveFaceDoesNotVisibleOr));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var activeTopology = new CubeTopologyState(FaceId.Floor);
                var inactiveTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                Assert.That(activeTopology.IsFaceActive(sourceCell.face), Is.True);
                Assert.That(inactiveTopology.IsFaceActive(sourceCell.face), Is.False);
                presenter.Initialize(binder, boardBounds, activeTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, activeTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: activeTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, activeTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: inactiveTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(activeTopology, inactiveTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(0f);

                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));
                Assert.That(stateStore.JumpDetachedVisibilityStates[20].AuthoritativeFace, Is.EqualTo(FaceId.Floor));
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.False);
                Assert.That(facts.IsJumpDetachedVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpWindupDoesNotRetainDetachedVisibility()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpWindupDoesNotRetainDetachedVisibility));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));
                presenter.UpdatePresentation(0f);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    tickIndex: 2,
                    new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyJumpPresentationSignal(
                                20,
                                sequence: 1,
                                phase: EnemyJumpPhase.Windup,
                                startedWindupThisTick: true,
                                startedAirborneThisTick: false,
                                landedThisTick: false,
                                retryThisTick: false,
                                sourceCell: sourceCell,
                                lockedTargetCell: landingCell,
                                presentationTargetCell: landingCell,
                                facing: Direction.Right,
                                windupTicks: 1,
                                landingTick: 4,
                                remainingAirborneTicks: 0,
                                retryCount: 0),
                        },
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologySuspendDoesNotReplaceJumpTrack()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologySuspendDoesNotReplaceJumpTrack));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 1.5f);
                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.TryGetValue(20, out var originalTrack), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(trackState.JumpTracks.TryGetValue(20, out var frozenTrack), Is.True);
                Assert.That(frozenTrack, Is.SameAs(originalTrack));
                Assert.That(frozenTrack.HasClip, Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(trackState.JumpTracks.TryGetValue(20, out var resumedFrozenTrack), Is.True);
                Assert.That(resumedFrozenTrack, Is.SameAs(originalTrack));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologySuspendDoesNotAdvanceFrozenPose()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologySuspendDoesNotAdvanceFrozenPose));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(0.01f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeSuspend = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                AssertPositionApproximately(view.transform.localPosition, poseBeforeSuspend);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpLandingCompletionTopologySuspendDoesNotAdvanceCompletion()
        {
            var rootObject = new GameObject(nameof(EnemyJumpLandingCompletionTopologySuspendDoesNotAdvanceCompletion));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 1, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpFreezePoseSurvivesFrontFaceInactiveVisibility()
        {
            var rootObject = new GameObject(nameof(EnemyJumpFreezePoseSurvivesFrontFaceInactiveVisibility));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(0.01f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeSuspend = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0.1f);

                var stateStore = GetPresentationStateStore(presenter);
                AssertPositionApproximately(view.transform.localPosition, poseBeforeSuspend);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: initialTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.25f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var airborneBeforeRotation = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(airborneBeforeRotation));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);
                var beforeRotation = GetAnimatorNormalizedTime(animator);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                var frozen = GetAnimatorNormalizedTime(animator);
                Assert.That(frozen, Is.EqualTo(beforeRotation).Within(0.0001f));

                for (var i = 0; i < 4; i++)
                {
                    presenter.UpdatePresentation(0.03f);
                    animator.Update(0.03f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(driver.IsPlaybackSuppressed, Is.True);
                Assert.That(animator.speed, Is.Zero);
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyTweenDoesNotReachExitTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime));
            var exitController = CreateJumpAirborneExitTimeAnimatorController(
                nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime),
                out var jumpClip,
                out var moveClip);
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime),
                out var jumpReferenceClip,
                exitController);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 1.25f);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);
                var frozen = GetAnimatorNormalizedTime(animator);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                for (var i = 0; i < 5; i++)
                {
                    presenter.UpdatePresentation(0.25f);
                    animator.Update(0.25f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(exitController);
                UnityEngine.Object.DestroyImmediate(jumpClip);
                UnityEngine.Object.DestroyImmediate(moveClip);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var frozen = GetAnimatorNormalizedTime(animator);

                for (var i = 0; i < 3; i++)
                {
                    presenter.UpdatePresentation(0.2f);
                    animator.Update(0.2f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 4));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 3));
                var frozen = GetAnimatorNormalizedTime(animator);
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                animator.Update(timingProfile.TopologyMotionDurationSeconds);
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 3,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(suspendedTopology, bottomTopology, CubeRotationKind.Backward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                animator.Update(timingProfile.TopologyMotionDurationSeconds);
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                presenter.UpdatePresentation(0f);
                Assert.That(animator.speed, Is.GreaterThan(0f));
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                animator.Update(0.1f);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(GetAnimatorNormalizedTime(animator), Is.GreaterThan(frozen));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime));
            var exitController = CreateJumpAirborneExitTimeAnimatorController(
                nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime),
                out var jumpClip,
                out var moveClip);
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime),
                out var jumpReferenceClip,
                exitController);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 1.25f);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                var frozen = GetAnimatorNormalizedTime(animator);
                animator.Update(1.1f);

                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(exitController);
                UnityEngine.Object.DestroyImmediate(jumpClip);
                UnityEngine.Object.DestroyImmediate(moveClip);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneFrontInactivePausesAirborneNotIdle()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneFrontInactivePausesAirborneNotIdle));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneFrontInactivePausesAirborneNotIdle),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(driver.IsPlaybackSuppressed, Is.True);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var signalCount = driver.JumpAirborneSignalCount;

                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(signalCount));
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 3,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(suspendedTopology, bottomTopology, CubeRotationKind.Backward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                presenter.UpdatePresentation(0f);

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                Assert.That(driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                view.gameObject.SetActive(false);
                animator.Rebind();
                animator.Update(0f);

                presenter.UpdatePresentation(0f);

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.ContainsKey(20), Is.True);
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit));
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpFallbackLanding_InterpolatesAndBlocksUntilCompletion()
        {
            var rootObject = new GameObject(
                "GameplayTickPresentationCoordinator_JumpFallbackLanding_InterpolatesAndBlocksUntilCompletion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
                var fallbackCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: lockedTargetCell,
                                    presentationTargetCell: lockedTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 4,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeLanding = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, fallbackCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: lockedTargetCell,
                                    presentationTargetCell: fallbackCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                var fallbackPosition = GetProjectedEntityPosition(boardBounds, topology, fallbackCell, EntityType.Unit);
                Assert.That(view.transform.localPosition.x, Is.GreaterThan(poseBeforeLanding.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(fallbackPosition.x - 0.001f));

                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsJumpLandingCompletionHeld, Is.True);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                AssertPositionApproximately(view.transform.localPosition, fallbackPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpExactLandingWithTopologyMotion_BlocksUntilLandingCompletion()
        {
            var rootObject = new GameObject(
                "GameplayTickPresentationCoordinator_JumpExactLandingWithTopologyMotion_BlocksUntilLandingCompletion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 1, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeLanding = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                var landingPosition = GetProjectedEntityPosition(boardBounds, rotatedTopology, landingCell, EntityType.Unit);
                AssertPositionApproximately(view.transform.localPosition, poseBeforeLanding);
                Assert.That(Vector3.Distance(view.transform.localPosition, landingPosition), Is.GreaterThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                AssertPositionApproximately(view.transform.localPosition, landingPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var firstTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
                var retryTargetCell = new SurfaceCell(FaceId.Floor, 3, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: firstTargetCell,
                                    presentationTargetCell: firstTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var midRetryPose = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    true,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: firstTargetCell,
                                    presentationTargetCell: retryTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 1),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                AssertPositionApproximately(view.transform.localPosition, midRetryPose);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                var retryTargetPosition = GetProjectedEntityPosition(boardBounds, topology, retryTargetCell, EntityType.Unit);
                Assert.That(view.transform.localPosition.x, Is.GreaterThan(midRetryPose.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(retryTargetPosition.x + 0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_NonJumpDetach_StillHides()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_NonJumpDetach_StillHides");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_ItemConsumeEffect_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float playerMoveOverrideSeconds = 0.4f;
                const float itemConsumeEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: itemConsumeEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var itemCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var motionAuthoring = playerViewPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();
                Assert.That(motionAuthoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "moveMotionDurationSeconds",
                    playerMoveOverrideSeconds);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                        CreateBox(20, itemCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, destinationCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                            },
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, itemCell, topology, Direction.Right),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, itemCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.ItemConsume,
                                    itemCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(registry.TryGetView(20, out var itemView), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(itemConsumeEffectDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                Assert.That(playerView.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float pushOverrideSeconds = 0.45f;
                const float boxDestroyEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: boxDestroyEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(20, boxCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                var motionAuthoring = boxView.gameObject.AddComponent<EntityMotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "pushMotionDurationSeconds",
                    pushOverrideSeconds);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, boxCell, topology, Direction.Right),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, boxCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.BoxDestroy,
                                    boxCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(boxView.gameObject.activeSelf, Is.False);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_KeepsOriginalViewVisibleUntilMotionCompletes()
        {
            var rootObject = new GameObject(
                "GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_KeepsOriginalViewVisibleUntilMotionCompletes");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(30, out var boxView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Push,
                                    sourceCell,
                                    destroyTileCell),
                            },
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    30,
                                    TickEntityExitCause.BoxDestroy,
                                    destroyTileCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    timing: EntityExitPresentationTiming.AfterEntityMotion),
                            })));

                Assert.That(boxView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destroyTileCell, EntityType.Box);
                var travel = destinationPosition - sourcePosition;
                var progress = Vector3.Dot(
                    boxView.transform.localPosition - sourcePosition,
                    travel.normalized);
                Assert.That(boxView.gameObject.activeSelf, Is.True);
                Assert.That(progress, Is.GreaterThan(0.001f));
                Assert.That(progress, Is.LessThan(travel.magnitude - 0.001f));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f + 0.01f);

                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_WithoutMotionFallsBackToImmediateHide()
        {
            var rootObject = new GameObject(
                "GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_WithoutMotionFallsBackToImmediateHide");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, boxCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(30, out var boxView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    30,
                                    TickEntityExitCause.BoxDestroy,
                                    boxCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    timing: EntityExitPresentationTiming.AfterEntityMotion),
                            })));

                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_FlipDestroySelfImpactTransient_HidesAuthoritativeView_WithoutCommittedMove()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FlipDestroySelfImpactTransient_HidesAuthoritativeView_WithoutCommittedMove");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float flipMotionDurationSeconds = 0.2f;
                const float boxDestroyEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: flipMotionDurationSeconds,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: boxDestroyEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, -1, 0);
                var impactCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(20, sourceCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.BoxDestroy,
                                    sourceCell,
                                    topology,
                                    Direction.Left,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            },
                            impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                            flipImpactSignals: new[]
                            {
                                new FlipImpactPresentationSignal(
                                    sourceActionPlanId: 1,
                                    boxEntityId: 20,
                                    impactTargetEntityId: 30,
                                    actorEntityId: 10,
                                    sourceCell,
                                    impactCell,
                                    topology,
                                    Direction.Left,
                                    Direction.Right,
                                    FlipImpactPresentationDisposition.DestroySelf),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(boxView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds + 0.01f);

                presenter.UpdatePresentation((flipMotionDurationSeconds - boxDestroyEffectDurationSeconds) + 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_PlayerAcceptedHit_DoesNotSpawnLegacyHitEffect()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerAcceptedHit_DoesNotSpawnLegacyHitEffect");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerAcceptedHit_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitEffectDurationSeconds", 0.2f);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab: playerViewPrefab));

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 2),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
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
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));

                presenter.UpdatePresentation(0.21f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldSource_DoesNotSpawnLegacyActiveLoop()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FrontFaceShieldSource_DoesNotSpawnLegacyActiveLoop");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_FrontFaceShieldActive",
                UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);
            var telegraphPrefab = new GameObject("FrontFaceShieldTelegraphPrefab");

            try
            {
                var authoring = enemyPrefab.gameObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "telegraphPrefab", telegraphPrefab);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateFrontFaceShieldPresentationData(
                            new[] { CreateShieldSourceSignal(20, sourceCell, topology, tickIndex: 1) },
                            Array.Empty<TickFrontFaceShieldBlockSignal>())));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldActiveLoop_20"), Is.EqualTo(0));
                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldWindup_20"), Is.EqualTo(0));
                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldTelegraph_20"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(telegraphPrefab);
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldMissingAuthoring_NoOps()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FrontFaceShieldMissingAuthoring_NoOps");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                Assert.DoesNotThrow(
                    () => presenter.Present(
                        CreateTickResult(
                            tickIndex: 1,
                            new[] { CreateEnemyUnit(20, sourceCell) },
                            topology,
                            CreateFrontFaceShieldPresentationData(
                                new[] { CreateShieldSourceSignal(20, sourceCell, topology, tickIndex: 1) },
                                new[]
                                {
                                    CreateShieldBlockSignal(
                                        shieldSourceEntityId: 20,
                                        boxEntityId: 30,
                                        actorEntityId: 10,
                                        blockedCell: sourceCell,
                                        shieldSourceCell: sourceCell,
                                        topology: topology,
                                        tickIndex: 1),
                                }))));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShield"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldNullPrefabs_NoOps()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FrontFaceShieldNullPrefabs_NoOps");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_FrontFaceShieldNullPrefabs",
                UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);

            try
            {
                enemyPrefab.gameObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                Assert.DoesNotThrow(
                    () => presenter.Present(
                        CreateTickResult(
                            tickIndex: 1,
                            new[] { CreateEnemyUnit(20, sourceCell) },
                            topology,
                            CreateFrontFaceShieldPresentationData(
                                new[] { CreateShieldSourceSignal(20, sourceCell, topology, tickIndex: 1) },
                                new[]
                                {
                                    CreateShieldBlockSignal(
                                        shieldSourceEntityId: 20,
                                        boxEntityId: 30,
                                        actorEntityId: 10,
                                        blockedCell: sourceCell,
                                        shieldSourceCell: sourceCell,
                                        topology: topology,
                                        tickIndex: 1),
                                }))));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShield"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldBlockSignal_DoesNotSpawnLegacyBurst()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FrontFaceShieldBlockSignal_DoesNotSpawnLegacyBurst");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_FrontFaceShieldBurst",
                UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);

            try
            {
                var authoring = enemyPrefab.gameObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "blockBurstSeconds", 0.2f);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateFrontFaceShieldPresentationData(
                            new[] { CreateShieldSourceSignal(20, sourceCell, topology, tickIndex: 1) },
                            new[]
                            {
                                CreateShieldBlockSignal(
                                    shieldSourceEntityId: 20,
                                    boxEntityId: 30,
                                    actorEntityId: 10,
                                    blockedCell: sourceCell,
                                    shieldSourceCell: sourceCell,
                                    topology: topology,
                                    tickIndex: 1),
                            })));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldBlockBurst_20_30"), Is.Zero);

                presenter.UpdatePresentation(0.21f);

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldBlockBurst_20_30"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldWindupWarning_DoesNotSpawnOldTelegraphPrefab()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FrontFaceShieldWindupWarning_DoesNotSpawnOldTelegraphPrefab");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_FrontFaceShieldWindup",
                UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                EntityMotionPresentationAuthoring.UseGlobalTimingSentinel);
            var telegraphPrefab = new GameObject("FrontFaceShieldTelegraphPrefab");

            try
            {
                var authoring = enemyPrefab.gameObject.AddComponent<EnemyFrontFaceShieldPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(authoring, "telegraphPrefab", telegraphPrefab);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateFrontFaceShieldPresentationData(
                            Array.Empty<TickFrontFaceShieldSourceSignal>(),
                            Array.Empty<TickFrontFaceShieldBlockSignal>(),
                            new[] { CreateShieldWindupWarningSignal(20, sourceCell, topology, tickIndex: 1) })));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldWindup_20_0_1"), Is.EqualTo(0));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateFrontFaceShieldPresentationData(
                            Array.Empty<TickFrontFaceShieldSourceSignal>(),
                            Array.Empty<TickFrontFaceShieldBlockSignal>())));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "FrontFaceShieldWindup_20_0_1"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(telegraphPrefab);
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_SummonWindupWarning_MissingAuthoringNoOps()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_SummonWindupWarning_MissingAuthoringNoOps");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                Assert.DoesNotThrow(
                    () => presenter.Present(
                        CreateTickResult(
                            tickIndex: 1,
                            new[] { CreateEnemyUnit(20, sourceCell) },
                            topology,
                            CreateSummonWindupPresentationData(
                                new[] { CreateSummonWindupWarningSignal(20, sourceCell, topology, tickIndex: 1) }))));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "SummonWindupWarning_20"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_FrontFaceShieldVfx_DoesNotParseLegacyEventStrings()
        {
            var hostRuntimeDirectory = Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Host/Runtime");
            var checkedFiles = new[]
            {
                Path.Combine(hostRuntimeDirectory, "GameplayTickPresentationCoordinator.cs"),
                Path.Combine(hostRuntimeDirectory, "GameplayFrontFaceShieldVfxPresenter.cs"),
            };

            foreach (var path in checkedFiles)
            {
                var source = File.ReadAllText(path);

                Assert.That(source, Does.Not.Contain("BoxSlideBlockedByFrontFaceShield"), path);
                Assert.That(source, Does.Not.Contain("PlayerActionBlockedByFrontFaceShield"), path);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_GravityFieldContinuousVisualState_RespectsArchitectureBoundaries()
        {
            var hostRuntimeDirectory = Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Host/Runtime");
            var loopRuntimeDirectory = Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Loop/Runtime");
            var gravityFieldVisualConsumers = new[]
            {
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualPresentationController.cs"),
                Path.Combine(hostRuntimeDirectory, "IGravityFieldVisualTarget.cs"),
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualTargetView.cs"),
                Path.Combine(hostRuntimeDirectory, "GravityFieldLockedTargetVisualTargetView.cs"),
            };

            foreach (var path in gravityFieldVisualConsumers)
            {
                var source = File.ReadAllText(path);

                Assert.That(source, Does.Not.Contain("CreateSnapshot"), path);
                Assert.That(source, Does.Not.Contain("WorldState"), path);
                Assert.That(source, Does.Not.Contain("Play3D"), path);
                Assert.That(source, Does.Not.Contain("StagePresentationDefinition"), path);
            }

            var gravityFieldVisualController = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualPresentationController.cs"));
            var gravityFieldTargetView = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualTargetView.cs"));
            var gravityFieldLockedTargetView = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldLockedTargetVisualTargetView.cs"));
            Assert.That(gravityFieldVisualController, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("GameplayCubeProjector"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("GameplayCubeProjector"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("TryProject"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("TryProject"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain(".material"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain(".material"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain(".materials"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain(".materials"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("sharedMaterial"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("sharedMaterial"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("sharedMaterials"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("sharedMaterials"));

            var coordinatorSource = File.ReadAllText(Path.Combine(hostRuntimeDirectory, "GameplayTickPresentationCoordinator.cs"));
            var presenterSource = File.ReadAllText(Path.Combine(hostRuntimeDirectory, "GameplayTickViewPresenter.cs"));
            Assert.That(coordinatorSource, Does.Not.Contain("WorldState.CreateSnapshot"));
            Assert.That(presenterSource, Does.Not.Contain("WorldState.CreateSnapshot"));

            var tickPipelineSource = File.ReadAllText(Path.Combine(loopRuntimeDirectory, "TickPipeline.cs"));
            Assert.That(tickPipelineSource, Does.Not.Contain("GravityFieldVisualTargetView"));
            Assert.That(tickPipelineSource, Does.Not.Contain("GravityFieldLockedTargetVisualTargetView"));
            Assert.That(tickPipelineSource, Does.Not.Contain("IGravityFieldContinuousVisualTarget"));
            Assert.That(tickPipelineSource, Does.Not.Contain("IGravityFieldLockedTargetVisualTarget"));
            Assert.That(tickPipelineSource, Does.Not.Contain("Play3D"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerDeathTick_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitEffectDurationSeconds", 0.2f);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab: playerViewPrefab));

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, playerCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    playerCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
                            }),
                        string.Empty,
                        TickTrace.Empty));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_Settles_AndClearsOnRespawn()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_Settles_AndClearsOnRespawn");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var enemyCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                        CreateEnemyUnit(20, enemyCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var rootPositionBeforeDeath = playerView.transform.localPosition;
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 0),
                            CreateEnemyUnit(20, enemyCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 20,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: true,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                Assert.That(
                    Vector3.Distance(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    GetPlayerDeathDisplacementTrackState(presenter, 10),
                    Is.EqualTo(PlayerDeathDisplacementTrackState.Animating));
                Assert.That(GetPresentationActivityInspector(presenter).HasActiveEntityPresentationClips(), Is.True);

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds);

                Assert.That(
                    GetPlayerDeathDisplacementTrackState(presenter, 10),
                    Is.EqualTo(PlayerDeathDisplacementTrackState.Settled));
                Assert.That(GetPresentationActivityInspector(presenter).HasActiveEntityPresentationClips(), Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell),
                            CreateEnemyUnit(20, enemyCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, playerCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                AssertPositionApproximately(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath);
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttackerProjectsAlongSurfaceTangent()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttackerProjectsAlongSurfaceTangent");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttacker_PlayerPrefab");
            var cameraObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_OutputCamera");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                outputCamera.transform.SetParent(rootObject.transform, worldPositionStays: false);
                outputCamera.transform.localPosition = new Vector3(0.35f, 0.5f, -4f);
                outputCamera.transform.localRotation = Quaternion.identity;

                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Front);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var playerCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachOutputCamera(outputCamera);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var rootPositionBeforeDeath = playerView.transform.localPosition;
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 0),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 999,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: true,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                var offset = playerView.ModelRoot.localPosition - modelRootPositionBeforeDeath;
                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                Assert.That(offset.sqrMagnitude, Is.GreaterThan(0.000001f));
                Assert.That(float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z), Is.False);

                var surfaceNormal = -(playerView.transform.localRotation * Vector3.forward).normalized;
                var tangentAlignment = Mathf.Abs(Vector3.Dot(offset.normalized, surfaceNormal));
                Assert.That(tangentAlignment, Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerActionHold_KeepsEntityMotionPhaseUntilHoldCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerActionHold_KeepsEntityMotionPhaseUntilHoldCompletes");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerActionHold_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);
                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);
                var holdDurationSeconds = driver.PushPresentationDurationSeconds;
                Assert.That(holdDurationSeconds, Is.GreaterThan(0.01f));

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Push,
                                    1,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false),
                            },
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.Present(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.None,
                                    0,
                                    startedThisTick: false,
                                    completedThisTick: true,
                                    canceledThisTick: false),
                            },
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(holdDurationSeconds + 0.05f);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerRespawn_SpawnVisibilityShowsExistingPlayerViewAgain()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerRespawn_SpawnVisibilityShowsExistingPlayerViewAgain");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerRespawn_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var spawnCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.gameObject.activeSelf, Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(10f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryGetView(10, out var respawnedView), Is.True);
                Assert.That(respawnedView, Is.SameAs(playerView));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var spawnCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.TryGetComponent<PlayerAnimatorDriver>(out var driver), Is.True);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    spawnCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
                            },
                            impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                            flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                            playerDeathHoldSignals: new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 1,
                                    eligibleTick: 2,
                                    remainingTicks: 1,
                                    startedThisTick: true),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));

                presenter.Present(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset()
        {
            var rootObject = new GameObject("DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var hiddenTopology = new CubeTopologyState(FaceId.Back);
                var respawnTopology = new CubeTopologyState(FaceId.Front);
                var spawnCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    initialTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    initialTopology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        Array.Empty<EntityState>(),
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, initialTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(10f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(hiddenTopology, initialTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);
                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, respawnTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, respawnTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryGetView(10, out var respawnedView), Is.True);
                Assert.That(respawnedView, Is.SameAs(playerView));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn()
        {
            var rootObject = new GameObject("DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var hiddenTopology = new CubeTopologyState(FaceId.Back);
                var respawnTopology = new CubeTopologyState(FaceId.Front);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var deathCell = new SurfaceCell(FaceId.Back, 0, 0);
                var respawnCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    hiddenTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, deathCell),
                    },
                    hiddenTopology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.TryGetComponent<PlayerAnimatorDriver>(out var driver), Is.True);
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, deathCell, hp: 0),
                        },
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 0,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: false,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.Unknown),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, deathCell, hiddenTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    deathCell,
                                    hiddenTopology,
                                    Direction.Right,
                                    EntityType.Unit),
                            })));
                presenter.UpdatePresentation(0f);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(hiddenTopology, initialTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        Array.Empty<EntityState>(),
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, respawnTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 5,
                        new[]
                        {
                            CreatePlayerUnit(10, respawnCell),
                        },
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, respawnCell, respawnTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
                AssertPositionApproximately(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath);
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentInitialStackedUnits_UsesSharedCenterPoseWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentInitialStackedUnits_UsesSharedCenterPoseWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var stackedCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, stackedCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = GetProjectedEntityPosition(boardBounds, topology, stackedCell, EntityType.Unit);
                var playerPosition = playerView.transform.localPosition;
                var enemyPosition = enemyView.transform.localPosition;

                AssertPositionApproximately(playerPosition, center);
                AssertPositionApproximately(enemyPosition, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_MoveIntoOccupiedCell_KeepsSharedCenterPoseWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveIntoOccupiedCell_KeepsSharedCenterPoseWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stackedCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                presenter.Present(
                    CreateMotionTickResult(
                        new[]
                        {
                            CreatePlayerUnit(10, stackedCell),
                            CreateEnemyUnit(20, stackedCell),
                        },
                        topology,
                        new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, stackedCell)));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = GetProjectedEntityPosition(boardBounds, topology, stackedCell, EntityType.Unit);
                var playerPosition = playerView.transform.localPosition;
                var enemyPosition = enemyView.transform.localPosition;

                AssertPositionApproximately(playerPosition, center);
                AssertPositionApproximately(enemyPosition, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayCenteredWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayCenteredWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var stackedCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var projector = new GameplayCubeProjector(boardBounds, 1f);

                Assert.That(projector.TryProjectEntityCell(stackedCell, topology, EntityType.Unit, out var projectedPose), Is.True);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, stackedCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = projectedPose.LocalPosition;
                var playerOffset = playerView.transform.localPosition - center;
                var enemyOffset = enemyView.transform.localPosition - center;

                Assert.That(playerOffset.sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(enemyOffset.sqrMagnitude, Is.LessThan(0.000001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedFrontEnemy_StoresFrontFactsWithInactiveSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedFrontEnemy_StoresFrontFactsWithInactiveSemantic");
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_CommittedFrontEnemy_FloatingPrefab",
                0.8f,
                0.8f);

            try
            {
                var floatingDriver = enemyPrefab.gameObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(floatingDriver, "target", enemyPrefab.ModelRoot);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.IsTransitionOnlyVisible, Is.False);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Front));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
                Assert.That(view.GetComponent<EnemyFloatingPresentationDriver>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_AppliesEnemyVisualSemanticStateToPresentationDrivers()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_AppliesEnemyVisualSemanticStateToPresentationDrivers));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayEntityPresentationApplier_SemanticDriverPrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.gameObject.AddComponent<RecordingEnemySemanticPresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var recordingDriver = view.GetComponent<RecordingEnemySemanticPresentationDriver>();
                Assert.That(recordingDriver, Is.Not.Null);
                Assert.That(recordingDriver.ApplyCount, Is.GreaterThan(0));
                Assert.That(recordingDriver.LastState.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(recordingDriver.LastState.ShouldPauseAutonomousPresentation, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_UtilityScalePulseFreezesDuringFrontFaceInactive()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_UtilityScalePulseFreezesDuringFrontFaceInactive));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_UtilityScalePulseFreezePrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.ModelRoot.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                enemyPrefab.gameObject.AddComponent<EnemyUtilityScalePulsePresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemyUtilityPresentationData(new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            20,
                            EnemyUtilityPresentationKind.SummonMinion,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 10),
                    })));
                presenter.UpdatePresentation(0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var modelRoot = view.ModelRoot;
                var inactiveController = view.GetComponent<EnemyInactiveVisualController>();
                Assert.That(inactiveController, Is.Not.Null);
                var activeScale = modelRoot.localScale;
                Assert.That(activeScale.x, Is.GreaterThan(0.4f));

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateEnemyUnit(20, frontCell) },
                    topology,
                    TickPresentationData.Empty));
                presenter.UpdatePresentation(0.5f);

                Assert.That(modelRoot.localScale.x, Is.EqualTo(activeScale.x).Within(0.0001f));
                Assert.That(modelRoot.localScale.y, Is.EqualTo(activeScale.y).Within(0.0001f));
                Assert.That(inactiveController.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(inactiveController.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));

                var revealBefore = inactiveController.CurrentInactiveNoiseReveal;
                inactiveController.AdvanceInactiveNoiseReveal(0.1f);
                Assert.That(inactiveController.CurrentInactiveNoiseReveal, Is.GreaterThan(revealBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_HardUtilityCancel_NormalizesScalePulse()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_HardUtilityCancel_NormalizesScalePulse));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_UtilityScalePulseCancelPrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.ModelRoot.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                enemyPrefab.gameObject.AddComponent<EnemyUtilityScalePulsePresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemyUtilityPresentationData(new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            20,
                            EnemyUtilityPresentationKind.SummonMinion,
                            EnemyUtilityPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 10),
                    })));
                presenter.UpdatePresentation(1.7f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var modelRoot = view.ModelRoot;
                Assert.That(modelRoot.localScale.x, Is.Not.EqualTo(0.4f).Within(0.0001f));

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemyUtilityPresentationData(new[]
                    {
                        new TickEnemyUtilityPresentationSignal(
                            20,
                            EnemyUtilityPresentationKind.SummonMinion,
                            EnemyUtilityPresentationPhase.Canceled,
                            startTick: 2,
                            executeTick: 2,
                            durationTicks: 0),
                    })));

                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        [TestCase(FaceId.Front, FaceId.Ceiling, GameplayProjectedFaceSlot.Top)]
        [TestCase(FaceId.Ceiling, FaceId.Back, GameplayProjectedFaceSlot.Back)]
        [TestCase(FaceId.Back, FaceId.Floor, GameplayProjectedFaceSlot.Bottom)]
        public void GameplayTickPresentationCoordinator_CommittedVisualFrontEnemy_StoresInactiveSemantic(FaceId bottomFace, FaceId enemyFace, GameplayProjectedFaceSlot expectedProjectedSlot)
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedVisualFrontEnemy_StoresInactiveSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(bottomFace);
                var enemyCell = new SurfaceCell(enemyFace, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, enemyCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(expectedProjectedSlot));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>().CurrentActivityState,
                    Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedTopEnemy_SuspendsFloatingWithoutFrontInactiveSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedTopEnemy_SuspendsFloatingWithoutFrontInactiveSemantic");
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_CommittedTopEnemy_FloatingPrefab",
                0.8f,
                0.8f);

            try
            {
                var floatingDriver = enemyPrefab.gameObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(floatingDriver, "target", enemyPrefab.ModelRoot);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var topCell = new SurfaceCell(FaceId.Ceiling, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, topCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                if (stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts))
                {
                    Assert.That(facts.IsEnemy, Is.True);
                    Assert.That(facts.IsVisible, Is.True);
                    Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Top));
                    Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                    Assert.That(facts.IsOnVisualFrontFace, Is.False);
                }

                if (stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic))
                {
                    Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                    Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                    Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
                }

                if (registry.TryGetView(20, out var view))
                {
                    Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                    Assert.That(view.GetComponent<EnemyFloatingPresentationDriver>(), Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedPhysicalFrontBottomEnemy_StoresNormalSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedPhysicalFrontBottomEnemy_StoresNormalSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Front));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedFrontRoleEnemyWithNoneAiMode_StoresInactiveEnemyFacts()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedFrontRoleEnemyWithNoneAiMode_StoresInactiveEnemyFacts");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell, EnemyAiMode.None) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedBottomRoleEnemyWithNoneAiMode_StoresUnsuppressedEnemyFacts()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedBottomRoleEnemyWithNoneAiMode_StoresUnsuppressedEnemyFacts");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell, EnemyAiMode.None) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_UnchangedEnemy_SkipsExpensiveApply()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_UnchangedEnemy_SkipsExpensiveApply));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out var registry, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.Zero);
                Assert.That(diagnostics.SignatureUnchangedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ChangedEnemyVisualState_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ChangedEnemyVisualState_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out var registry, out var topology);
                presenter.PresentInitial(
                    new[] { CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    topology);
                presenter.UpdatePresentation(0f);

                var frontEnemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Front, 0, 0));
                presenter.Present(CreateTickResult(1, new[] { frontEnemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.SignatureChangedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>().CurrentActivityState,
                    Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ChangedPose_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ChangedPose_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(
                    rootObject,
                    out var registry,
                    out var topology,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)));
                presenter.PresentInitial(
                    new[] { CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    topology);
                presenter.UpdatePresentation(0f);

                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var movedEnemy = CreateEnemyUnit(20, destinationCell);
                presenter.Present(CreateTickResult(1, new[] { movedEnemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                        topology,
                        destinationCell,
                        EntityType.Unit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ActiveMotion_DoesNotSkip()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ActiveMotion_DoesNotSkip));

            try
            {
                var presenter = CreatePrimitivePresenter(
                    rootObject,
                    out _,
                    out var topology,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)));
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateMotionTickResult(
                    CreateEnemyUnit(20, destinationCell),
                    topology,
                    sourceCell,
                    destinationCell,
                    TickEntityMotionKind.Move));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_NewlyBoundEntity_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_NewlyBoundEntity_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.Present(CreateTickResult(1, new[] { enemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_VisibilityChanged_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_VisibilityChanged_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, cell) }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateVisibilityPresentationData(new TickVisibilityChange(
                        20,
                        TickVisibilityChangeKind.Remove,
                        cell,
                        topology,
                        Direction.Right))));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_PresentationEventTarget_DoesNotSkip()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_PresentationEventTarget_DoesNotSkip));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { enemy },
                    topology,
                    CreateEnemyDamagePresentationData(new TickEnemyDamagePresentationSignal(20, true, 1))));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ActiveBypassCount, Is.EqualTo(1));
                Assert.That(diagnostics.SignatureUnchangedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_PlayerPath_Preserved()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_PlayerPath_Preserved));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var player = CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0));
                presenter.PresentInitial(new[] { player }, topology);

                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.Zero);
                Assert.That(diagnostics.SkippedCount, Is.Zero);
                Assert.That(diagnostics.PlayerExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PresentationDirtyOnly_Diagnostics_CountsSkippedAndExecuted()
        {
            var rootObject = new GameObject(nameof(PresentationDirtyOnly_Diagnostics_CountsSkippedAndExecuted));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var entities = new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)),
                };
                presenter.PresentInitial(entities, topology);

                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.CandidateCount, Is.EqualTo(2));
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlayerExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.SkippedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_CommittedFrontEnemy_ResolvesFrontFaceInactive()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: true);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_TransitionFrontEnemy_ResolvesFrontFaceInactive()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: false,
                isTransitionVisible: true,
                isTransitionOnlyVisible: true,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: true);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_PhysicalFrontBottomEnemy_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void DefaultEnemyVisualSemanticResolver_PhysicalFrontSuppressedNonVisualFront_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_VisibleNonFrontEnemy_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_VisibleSuppressedNonFrontEnemy_PausesAutonomousPresentation()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_JumpLandingCompletionHold_PausesAnimatorPlayback()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: true,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_AppliesInactiveBlendAndColorOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_AppliesInactiveBlendAndColorOverride");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.ConfigureLegacyColorFallback(true);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(controller.CurrentInactiveBlend, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_DefaultPolicy_DoesNotApplyLegacyBaseColorOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_DefaultPolicy_DoesNotApplyLegacyBaseColorOverride");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/3Startis/Startis.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                var expectedLegacyColor = ResolveExpectedInactiveColor(
                    material.GetColor("_BaseColor"),
                    inactiveTint: new Color(0.62f, 0.64f, 0.68f, 1f),
                    desaturateStrength: 0.85f,
                    inactiveBlend: 1f);

                Assert.That(controller.AllowLegacyColorFallback, Is.False);
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.Not.EqualTo(expectedLegacyColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_StopsAndClearsChildParticles()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_StopsAndClearsChildParticles");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                var particleObject = new GameObject("ChildParticles");
                particleObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var particles = particleObject.AddComponent<ParticleSystem>();
                particles.Play(withChildren: true);
                particles.Emit(5);

                Assert.That(particles.particleCount, Is.GreaterThan(0));

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(particles.isPlaying, Is.False);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_ConfigureSettings_AppliesInactiveTintAndStrengths()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_ConfigureSettings_AppliesInactiveTintAndStrengths));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.25f, 0.5f, 0.75f, 1f),
                desaturateStrength: 0.35f,
                emissionSuppression: 0.45f);

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Configure(settings);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                AssertColorApproximately(
                    new Color(0.25f, 0.5f, 0.75f, 1f),
                    GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionSuppressionProperty), Is.EqualTo(0.45f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_ConfigureSettings_DoesNotResetRevealState()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_ConfigureSettings_DoesNotResetRevealState));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.1f, 0.2f, 0.3f, 1f),
                desaturateStrength: 0.4f,
                emissionSuppression: 0.5f);

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);
                var revealBeforeConfigure = controller.CurrentInactiveNoiseReveal;

                controller.Configure(settings);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(revealBeforeConfigure).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_FrontFaceInactive_StartsNoiseReveal()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_FrontFaceInactive_StartsNoiseReveal));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                Assert.That(controller, Is.InstanceOf<IEnemyVisualSemanticPresentationDriver>());

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(controller.CurrentInactiveBlend, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_FrontFaceInactive_AdvancesRevealIn()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_FrontFaceInactive_AdvancesRevealIn));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f + 0.01f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_Clear_StartsRevealOutButKeepsGate()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_Clear_StartsRevealOutButKeepsGate));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_Clear_AdvancesRevealOutAndDisablesGateAfterCompletion()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_Clear_AdvancesRevealOutAndDisablesGateAfterCompletion));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealOutSeconds * 0.5f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealOutSeconds * 0.5f + 0.01f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.False);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_OnDisable_ResetsRevealImmediately()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_OnDisable_ResetsRevealImmediately));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);

                typeof(EnemyInactiveVisualController)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, Array.Empty<object>());

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.False);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_PreservesExistingPropertyBlockValues()
        {
            const string existingPropertyName = "_ExistingEnemyInactiveMpbValue";
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_PreservesExistingPropertyBlockValues));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var existingBlock = new MaterialPropertyBlock();
                existingBlock.SetFloat(existingPropertyName, 0.75f);
                renderer.SetPropertyBlock(existingBlock);

                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, existingPropertyName), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_NullAndEmptyRenderers_NoOp()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_NullAndEmptyRenderers_NoOp));

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                PlayerViewPrefabTestUtility.SetSerializedField(controller, "targetRenderers", new Renderer[] { null });

                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive)));
                Assert.DoesNotThrow(() => controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds));
                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal)));

                PlayerViewPrefabTestUtility.SetSerializedField(controller, "targetRenderers", Array.Empty<Renderer>());

                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive)));
                Assert.DoesNotThrow(() => controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds));
                Assert.DoesNotThrow(() => controller.ResetVisual());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveEnemy_AppliesInactiveVisualSettingsAndKeepsLegacyFallback()
        {
            var rootObject = new GameObject(nameof(DefaultGameplayEntityViewFactory_PrimitiveEnemy_AppliesInactiveVisualSettingsAndKeepsLegacyFallback));
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.18f, 0.28f, 0.38f, 1f),
                desaturateStrength: 0.22f,
                emissionSuppression: 0.66f);

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyInactiveVisualSettings: settings);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);
                var renderer = view.GetComponentInChildren<Renderer>();

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller.AllowLegacyColorFallback, Is.True);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                AssertColorApproximately(new Color(0.18f, 0.28f, 0.38f, 1f), GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionSuppressionProperty), Is.EqualTo(0.66f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PrefabEnemy_AppliesInactiveVisualSettingsWithoutLegacyFallback()
        {
            var rootObject = new GameObject(nameof(DefaultGameplayEntityViewFactory_PrefabEnemy_AppliesInactiveVisualSettingsWithoutLegacyFallback));
            var prefabObject = new GameObject("EnemyPrefabWithInactiveSettings");
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.42f, 0.33f, 0.24f, 1f),
                desaturateStrength: 0.31f,
                emissionSuppression: 0.72f);

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(prefabObject.transform, worldPositionStays: false);

                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        [20] = prefabView,
                    },
                    enemyInactiveVisualSettings: settings);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);
                var renderer = view.GetComponentInChildren<Renderer>();

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller.AllowLegacyColorFallback, Is.False);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                AssertColorApproximately(new Color(0.42f, 0.33f, 0.24f, 1f), GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.31f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionSuppressionProperty), Is.EqualTo(0.72f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveEnemy_EnablesLegacyColorFallback()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_PrimitiveEnemy_EnablesLegacyColorFallback");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.AllowLegacyColorFallback, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveRoleEnemyWithNoneAiMode_AddsEnemyPresentationComponents()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_PrimitiveRoleEnemyWithNoneAiMode_AddsEnemyPresentationComponents");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None);
                var view = factory.CreateView(enemy);

                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StartisPrefabEnemy_KeepsLegacyColorFallbackDisabled()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_StartisPrefabEnemy_KeepsLegacyColorFallbackDisabled");

            try
            {
                var startisPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(
                    StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Startis.prefab");
                Assert.That(startisPrefab, Is.Not.Null);

                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        [20] = startisPrefab,
                    });

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.AllowLegacyColorFallback, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence()
        {
            var rootObject = new GameObject("EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence");

            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/2BlackEye/BE_LS_M1.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var controller = rootObject.AddComponent<EnemyPupilVisualController>();

                controller.Advance(0f);
                var defaultBorder = controller.CurrentBorder;
                Assert.That(defaultBorder, Is.EqualTo(0.44f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0.5f);
                Assert.That(controller.CurrentBorder, Is.GreaterThan(0.44f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0f);
                var recoveryBorder = controller.CurrentBorder;
                Assert.That(recoveryBorder, Is.EqualTo(0.22f).Within(0.0001f));

                controller.Advance(0.02f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(recoveryBorder).Within(0.0001f));

                controller.Advance(0.5f);
                Assert.That(controller.CurrentBorder, Is.GreaterThanOrEqualTo(recoveryBorder));
                Assert.That(controller.CurrentBorder, Is.LessThan(defaultBorder));

                controller.Advance(1f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(defaultBorder).Within(0.0001f));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(defaultBorder).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_NormalState_PreservesPupilBorderOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_NormalState_PreservesPupilBorderOverride");

            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/2BlackEye/BE_LS_M1.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var pupilController = rootObject.AddComponent<EnemyPupilVisualController>();
                var inactiveController = rootObject.AddComponent<EnemyInactiveVisualController>();

                pupilController.Advance(0f);
                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));
                pupilController.Advance(0f);
                inactiveController.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);

                Assert.That(pupilController.CurrentBorder, Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathExitEffectPlanBuilder_Build_UsesStableSeededResult()
        {
            var parentObject = new GameObject("EnemyDeathExitEffectPlanBuilder_Build_UsesStableSeededResult");
            var cameraObject = new GameObject("EnemyDeathExitEffectPlanBuilder_Build_OutputCamera");

            try
            {
                var outputCamera = cameraObject.AddComponent<Camera>();
                outputCamera.transform.position = new Vector3(0.5f, 0.25f, -10f);
                outputCamera.transform.rotation = Quaternion.identity;
                outputCamera.orthographic = true;
                outputCamera.orthographicSize = 3f;
                outputCamera.nearClipPlane = 0.1f;
                outputCamera.farClipPlane = 50f;

                var sourcePose = new GameplayEntityPose(new Vector3(0f, 0f, 0f), Quaternion.identity);
                var targetPose = new GameplayEntityPose(new Vector3(1.25f, 0.5f, 0f), Quaternion.identity);
                var firstPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    12345);
                var secondPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    12345);
                var differentSeedPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    54321);
                var startCameraLocalPosition = outputCamera.transform.InverseTransformPoint(
                    parentObject.transform.TransformPoint(sourcePose.Position));
                var targetCameraLocalPosition = outputCamera.transform.InverseTransformPoint(
                    parentObject.transform.TransformPoint(firstPlan.TargetLocalPosition));

                AssertPositionApproximately(firstPlan.TargetLocalPosition, secondPlan.TargetLocalPosition);
                Assert.That(firstPlan.ArcHeight, Is.EqualTo(secondPlan.ArcHeight).Within(0.0001f));
                Assert.That(firstPlan.SpinDegrees, Is.EqualTo(secondPlan.SpinDegrees).Within(0.0001f));
                Assert.That(targetCameraLocalPosition.z, Is.LessThan(startCameraLocalPosition.z));
                Assert.That(targetCameraLocalPosition.z, Is.GreaterThan(outputCamera.nearClipPlane));
                Assert.That(targetCameraLocalPosition.z, Is.LessThan(outputCamera.nearClipPlane + 0.25f));

                var hasDifferentTarget = Vector3.Distance(firstPlan.TargetLocalPosition, differentSeedPlan.TargetLocalPosition) > 0.001f;
                var hasDifferentArc = Mathf.Abs(firstPlan.ArcHeight - differentSeedPlan.ArcHeight) > 0.001f;
                var hasDifferentSpin = Mathf.Abs(firstPlan.SpinDegrees - differentSeedPlan.SpinDegrees) > 0.001f;
                Assert.That(hasDifferentTarget || hasDifferentArc || hasDifferentSpin, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        private static GameplayTimingProfile CreateTimingProfile(float topologyMotionDurationSeconds = 0.2f)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: topologyMotionDurationSeconds,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }

        private static Color ResolveExpectedInactiveColor(
            Color sourceColor,
            Color inactiveTint,
            float desaturateStrength,
            float inactiveBlend)
        {
            var luminance = (sourceColor.r * 0.2126f) + (sourceColor.g * 0.7152f) + (sourceColor.b * 0.0722f);
            var grayscale = new Color(luminance, luminance, luminance, sourceColor.a);
            var tinted = Color.Lerp(grayscale, inactiveTint, inactiveBlend * 0.35f);
            var desaturated = Color.Lerp(sourceColor, tinted, inactiveBlend * desaturateStrength);
            desaturated.a = sourceColor.a;
            return desaturated;
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position, EnemyAiMode aiMode = EnemyAiMode.Patrol)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState WithBoardPresence(EntityState entity, EntityBoardPresence boardPresence)
        {
            entity.boardPresence = boardPresence;
            return entity;
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static TickResult CreateMotionTickResult(
            EntityState finalEntity,
            CubeTopologyState topology,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            TickEntityMotionKind motionKind)
        {
            return CreateMotionTickResult(
                new[]
                {
                    finalEntity,
                },
                topology,
                new TickEntityMotion(finalEntity.entityId, motionKind, sourceCell, destinationCell));
        }

        private static TickResult CreateMotionTickResult(
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            params TickEntityMotion[] motions)
        {
            return CreateTickResult(
                tickIndex: 1,
                finalEntities,
                topology,
                new TickPresentationData(motions));
        }

        private static GameplayTickViewPresenter CreateInitializedPresenter(
            GameObject rootObject,
            out CubeTopologyState topology)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            topology = new CubeTopologyState(FaceId.Floor);

            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                topology,
                1f,
                CreateTimingProfile());
            presenter.PresentInitial(Array.Empty<EntityState>(), topology);
            return presenter;
        }

        private static GameplayTickViewPresenter CreatePrimitivePresenter(
            GameObject rootObject,
            out GameplayEntityViewRegistry registry,
            out CubeTopologyState topology,
            BoardBounds? boardBounds = null)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10));
            topology = new CubeTopologyState(FaceId.Floor);
            presenter.Initialize(
                binder,
                boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                topology,
                1f,
                CreateTimingProfile());
            presenter.PresentInitial(Array.Empty<EntityState>(), topology);
            return presenter;
        }

        private sealed class RecordingInitialPresentationExtension :
            IGameplayTickPresentationExtension,
            IGameplayInitialPresentationExtension
        {
            public int InitialPresentCallCount { get; private set; }

            public int TickPresentCallCount { get; private set; }

            public InitialPresentationData CapturedInitialPresentationData { get; private set; }

            public bool HadPlayerViewDuringInitial { get; private set; }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
                TickPresentCallCount++;
            }

            public void PresentInitial(in GameplayInitialPresentationExtensionContext context)
            {
                InitialPresentCallCount++;
                CapturedInitialPresentationData = context.PresentationData;
                HadPlayerViewDuringInitial =
                    context.StateStore != null &&
                    context.StateStore.ViewsByEntityId.ContainsKey(10);
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
            }
        }

        private sealed class RecordingPausablePresentationExtension :
            IGameplayTickPresentationExtension,
            IGameplayPresentationPausable
        {
            public int UpdateCallCount { get; private set; }

            public float AdvancedSeconds { get; private set; }

            public bool IsPaused { get; private set; }

            public int PauseCallCount { get; private set; }

            public int ResumeCallCount { get; private set; }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdateCallCount++;
                AdvancedSeconds += deltaTime;
            }

            public void HardCleanup()
            {
            }

            public void SetPresentationPaused(bool paused)
            {
                IsPaused = paused;
                if (paused)
                {
                    PauseCallCount++;
                    return;
                }

                ResumeCallCount++;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, args);
        }

        private static TileFeatureVisualTargetView AttachTileVisualTarget(
            GameObject rootObject,
            GameplayTickViewPresenter presenter,
            int tileId,
            SurfaceCell cell)
        {
            var registry = rootObject.GetComponent<TileFeatureVisualRegistry>() ??
                rootObject.AddComponent<TileFeatureVisualRegistry>();
            var targetObject = new GameObject($"TileFeatureVisualTarget_{tileId}");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var target = targetObject.AddComponent<TileFeatureVisualTargetView>();
            target.Configure(tileId, cell);
            registry.ConfigureSearchRoot(rootObject.transform);
            presenter.AttachTileFeatureVisualRegistry(registry);
            return target;
        }

        private static TickPresentationData CreateTilePresentationData(params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }

        private static TickPresentationData CreateTilePresentationDataWithTopologyMotion(
            TickTopologyMotion topologyMotion,
            params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return CreateGravityFieldPresentationData(
                Array.Empty<GravityFieldVisualState>(),
                gravityFieldEvents);
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            IReadOnlyList<GravityFieldVisualState> gravityFieldVisualStates,
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates);
        }

        private static TickPresentationData CreateGravityFieldVisualPresentationData(
            params GravityFieldVisualState[] gravityFieldVisualStates)
        {
            return CreateGravityFieldPresentationData(
                gravityFieldVisualStates,
                Array.Empty<GravityFieldPresentationEvent>());
        }

        private static TickPresentationData CreateVisibilityPresentationData(
            params TickVisibilityChange[] visibilityChanges)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: visibilityChanges,
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyDamagePresentationData(
            params TickEnemyDamagePresentationSignal[] enemyDamageSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: enemyDamageSignals,
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>());
        }

        private static GravityFieldVisualState CreateGravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            IReadOnlyList<SurfaceCell> areaCells = null,
            int slotVisibilityMask = 0,
            IReadOnlyList<int> lockedTargetEntityIds = null)
        {
            var progress = durationTicks <= 0
                ? 0f
                : (durationTicks - Math.Max(0, timerTicks)) / (float)durationTicks;
            return new GravityFieldVisualState(
                emitterEntityId,
                cell,
                phase,
                Math.Max(0, timerTicks),
                durationTicks,
                progress,
                areaCells == null && slotVisibilityMask == 0
                    ? GravityFieldAreaFootprint.Empty
                    : new GravityFieldAreaFootprint(areaCells ?? Array.Empty<SurfaceCell>(), slotVisibilityMask),
                lockedTargetEntityIds ?? Array.Empty<int>());
        }

        private static TilePresentationEvent CreateButtonActivatedTileEvent(int tileId, SurfaceCell cell)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.ButtonActivated,
                tileId,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationEvent CreateDestroyTileTriggeredTileEvent(
            int tileId,
            SurfaceCell cell,
            int targetEntityId)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.DestroyTileTriggered,
                tileId,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: targetEntityId);
        }

        private static TilePresentationEvent CreateMoonBlockGeneratedTileEvent(
            int tileId,
            SurfaceCell cell,
            int moonBlockEntityId,
            int spawnTick)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.MoonBlockGenerated,
                tileId,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: moonBlockEntityId,
                direction: Direction.None,
                spawnTick: spawnTick,
                spawnInteractionLockTicks: MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks);
        }

        private static TilePresentationRequest CreateMoonBlockGeneratedRequest(int moonBlockEntityId, int spawnTick)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.MoonBlockGenerated,
                tileId: 100,
                cell: new SurfaceCell(FaceId.Floor, 1, 1),
                tileFeatureKind: TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: 101,
                ownerEntityId: 102,
                teamId: 7,
                targetEntityId: moonBlockEntityId,
                direction: Direction.None,
                spawnTick: spawnTick,
                spawnInteractionLockTicks: MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks);
        }

        private static TickPresentationData CreateKinematicPresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickKinematicMotionTrack> kinematicMotionTracks,
            IReadOnlyList<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IReadOnlyList<TickEnemyGlidePresentationSignal> enemyGlideSignals = null)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                kinematicMotionTracks: kinematicMotionTracks,
                playerDeathHoldSignals: playerDeathHoldSignals,
                enemyGlideSignals: enemyGlideSignals);
        }

        private static TickPresentationData CreateGlidePresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickEnemyGlidePresentationSignal> enemyGlideSignals)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                enemyGlideSignals: enemyGlideSignals);
        }

        private static TickPresentationData CreateContinuousPresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickContinuousLocomotionTrack> continuousLocomotionTracks)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                continuousLocomotionTracks: continuousLocomotionTracks);
        }

        private static TickKinematicMotionTrack CreateKinematicTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            int sourceLocalX,
            SurfaceCell destinationAnchorCell,
            int destinationLocalX,
            CubeTopologyState topology,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
        {
            return new TickKinematicMotionTrack(
                entityId,
                sourceAnchorCell,
                CreateKinematicOffset(sourceLocalX, 0),
                destinationAnchorCell,
                CreateKinematicOffset(destinationLocalX, 0),
                terminalKind == TickKinematicMotionTerminalKind.None
                    ? MotionMode.Voluntary
                    : terminalKind == TickKinematicMotionTerminalKind.Interrupted
                        ? MotionMode.Interrupted
                        : MotionMode.Voluntary,
                ForcedMotionOp.None,
                EntityType.Unit,
                topology,
                topology,
                Direction.Right,
                Direction.Right,
                terminalKind);
        }

        private static TickContinuousLocomotionTrack CreateContinuousTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            int sourceLocalX,
            SurfaceCell destinationAnchorCell,
            int destinationLocalX,
            ContinuousLocomotionMode mode,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
        {
            return new TickContinuousLocomotionTrack(
                entityId,
                sourceAnchorCell,
                CreateKinematicOffset(sourceLocalX, 0),
                destinationAnchorCell,
                CreateKinematicOffset(destinationLocalX, 0),
                Direction.Right,
                Direction.Right,
                mode,
                terminalKind);
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }

        private static Vector3 GetProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.LocalPosition;
        }

        private static Vector3 GetProjectedEntityNormal(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.Normal;
        }

        private static TickEnemyGlidePresentationSignal CreateGlideSignal(
            int entityId,
            SurfaceCell anchorCell,
            EnemyGlidePhase phase,
            int currentHeightUnits)
        {
            return new TickEnemyGlidePresentationSignal(
                entityId,
                anchorCell,
                phase,
                sequence: 1,
                phaseElapsedTicks: 0,
                phaseTotalTicks: 1,
                normalizedPhaseProgress: 0f,
                liftHeightUnits: KinematicFixed.UnitsPerCell / 4,
                recoveryDipHeightUnits: 0,
                currentHeightUnits,
                isAirborneVisual: currentHeightUnits != 0,
                wantsRecover: false,
                isTerminalZero: currentHeightUnits == 0);
        }

        private static Vector3 GetProjectedKinematicEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType,
            int localX,
            int localY)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            var localOffset = CreateKinematicOffset(localX, localY);
            return projectedPose.LocalPosition +
                   (projectedPose.LocalRotation * new Vector3(
                       localOffset.X.RawValue / (float)KinematicFixed.UnitsPerCell,
                       localOffset.Y.RawValue / (float)KinematicFixed.UnitsPerCell,
                       0f));
        }

        private static KinematicOffset2 CreateKinematicOffset(int localX, int localY)
        {
            return new KinematicOffset2(
                KinematicFixed.FromRaw(localX),
                KinematicFixed.FromRaw(localY));
        }

        private static Vector3 ResolveLinearPosition(
            Vector3 source,
            Vector3 destination,
            float elapsedSeconds,
            float durationSeconds)
        {
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            return Vector3.LerpUnclamped(source, destination, normalizedTime);
        }

        private static Vector3 ResolveEasedLinearPosition(
            Vector3 source,
            Vector3 destination,
            float elapsedSeconds,
            float durationSeconds)
        {
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 2f);
            return Vector3.LerpUnclamped(source, destination, easedTime);
        }

        private static void AssertPositionApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static void AssertScaleApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static int GetJumpDetachedVisibilityStateCount(GameplayTickViewPresenter presenter)
        {
            return GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.Count;
        }

        private static GameplayPresentationStateStore GetPresentationStateStore(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            var stateStoreField = coordinator.GetType()
                .GetField("_stateStore", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stateStoreField, Is.Not.Null);
            return (GameplayPresentationStateStore)stateStoreField.GetValue(coordinator);
        }

        private static GameplayPresentationTrackState GetPresentationTrackState(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            var trackStateField = coordinator.GetType()
                .GetField("_trackState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(trackStateField, Is.Not.Null);
            return (GameplayPresentationTrackState)trackStateField.GetValue(coordinator);
        }

        private static GameplayPresentationActivityInspector GetPresentationActivityInspector(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            var inspectorField = coordinator.GetType()
                .GetField("_presentationActivityInspector", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(inspectorField, Is.Not.Null);
            return (GameplayPresentationActivityInspector)inspectorField.GetValue(coordinator);
        }

        private static bool HasPlayerDeathDisplacementTrack(GameplayTickViewPresenter presenter, int entityId)
        {
            return GetPresentationTrackState(presenter).PlayerDeathDisplacementTracks.ContainsKey(entityId);
        }

        private static PlayerDeathDisplacementTrackState GetPlayerDeathDisplacementTrackState(
            GameplayTickViewPresenter presenter,
            int entityId)
        {
            Assert.That(
                GetPresentationTrackState(presenter).PlayerDeathDisplacementTracks.TryGetValue(entityId, out var track),
                Is.True);
            Assert.That(track, Is.Not.Null);
            return track.State;
        }

        private static object GetPresentationCoordinator(GameplayTickViewPresenter presenter)
        {
            var coordinatorField = typeof(GameplayTickViewPresenter)
                .GetField("_presentationCoordinator", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(coordinatorField, Is.Not.Null);
            return coordinatorField.GetValue(presenter);
        }

        private static GameplayEntityViewBinder CreateEnemyPrefabBinder(
            GameplayEntityViewRegistry registry,
            GameplayEntityView enemyPrefab)
        {
            return new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 20, enemyPrefab },
                    }));
        }

        private static TickResult CreateJumpAirborneTick(
            int tickIndex,
            CubeTopologyState finalTopology,
            EntityState finalEntity,
            TickTopologyMotion? topologyMotion,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            SurfaceCell sourceCell,
            SurfaceCell landingCell,
            bool startedAirborneThisTick,
            int remainingAirborneTicks)
        {
            return CreateTickResult(
                tickIndex,
                new[] { finalEntity },
                finalTopology,
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion,
                    visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            20,
                            sequence: 1,
                            phase: EnemyJumpPhase.Airborne,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: startedAirborneThisTick,
                            landedThisTick: false,
                            retryThisTick: false,
                            sourceCell: sourceCell,
                            lockedTargetCell: landingCell,
                            presentationTargetCell: landingCell,
                            facing: Direction.Right,
                            landingTick: tickIndex + remainingAirborneTicks,
                            remainingAirborneTicks: remainingAirborneTicks,
                            retryCount: 0),
                    },
                    Array.Empty<TickEntityExitPresentationSignal>()));
        }

        private static TickPresentationData CreateFrontFaceShieldPresentationData(
            IReadOnlyList<TickFrontFaceShieldSourceSignal> sources,
            IReadOnlyList<TickFrontFaceShieldBlockSignal> blocks,
            IReadOnlyList<TickFrontFaceShieldWindupWarningSignal> windupWarnings = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                frontFaceShieldSources: sources,
                frontFaceShieldBlocks: blocks,
                frontFaceShieldWindupWarnings: windupWarnings ?? Array.Empty<TickFrontFaceShieldWindupWarningSignal>());
        }

        private static TickPresentationData CreateSummonWindupPresentationData(
            IReadOnlyList<TickSummonWindupWarningSignal> summonWindupWarnings)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                summonWindupWarnings: summonWindupWarnings);
        }

        private static TickPresentationData CreateEnemyUtilityPresentationData(
            IReadOnlyList<TickEnemyUtilityPresentationSignal> enemyUtilitySignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                enemyUtilitySignals: enemyUtilitySignals);
        }

        private static TickFrontFaceShieldSourceSignal CreateShieldSourceSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int tickIndex,
            int radius = 1,
            FrontFaceShieldTargetPattern targetPattern = FrontFaceShieldTargetPattern.ManhattanRadius)
        {
            return new TickFrontFaceShieldSourceSignal(
                sourceEntityId,
                sourceCell,
                topology,
                radius,
                false,
                targetPattern,
                tickIndex,
                presentationSeed: tickIndex * 31 + sourceEntityId);
        }

        private static TickFrontFaceShieldBlockSignal CreateShieldBlockSignal(
            int shieldSourceEntityId,
            int boxEntityId,
            int actorEntityId,
            SurfaceCell blockedCell,
            SurfaceCell shieldSourceCell,
            CubeTopologyState topology,
            int tickIndex)
        {
            return new TickFrontFaceShieldBlockSignal(
                shieldSourceEntityId,
                boxEntityId,
                actorEntityId,
                blockedCell,
                shieldSourceCell,
                FrontFaceShieldBlockMovementKind.PushStart,
                topology,
                tickIndex,
                presentationSeed: tickIndex * 31 + shieldSourceEntityId + boxEntityId);
        }

        private static TickFrontFaceShieldWindupWarningSignal CreateShieldWindupWarningSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int tickIndex)
        {
            return new TickFrontFaceShieldWindupWarningSignal(
                sourceEntityId,
                0,
                sourceCell,
                topology,
                1,
                false,
                FrontFaceShieldTargetPattern.ManhattanRadius,
                tickIndex,
                tickIndex + 1,
                1,
                tickIndex,
                tickIndex * 31 + sourceEntityId);
        }

        private static TickSummonWindupWarningSignal CreateSummonWindupWarningSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int tickIndex)
        {
            return new TickSummonWindupWarningSignal(
                sourceEntityId,
                0,
                sourceCell,
                topology,
                Direction.Right,
                tickIndex,
                tickIndex + 1,
                1,
                tickIndex,
                tickIndex * 31 + sourceEntityId);
        }

        private static GameObject[] CreateAreaSlots(Transform parent, int slotCount)
        {
            var slots = new GameObject[slotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"AreaSlot_{i}");
                slot.transform.SetParent(parent, worldPositionStays: false);
                slots[i] = slot;
            }

            return slots;
        }

        private static int CountDescendantsByNamePrefix(Transform root, string prefix)
        {
            var count = 0;
            var descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null &&
                    descendants[i] != root &&
                    descendants[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryFindDescendantByNamePrefix(Transform root, string prefix, out Transform descendant)
        {
            var descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null &&
                    descendants[i] != root &&
                    descendants[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    descendant = descendants[i];
                    return true;
                }
            }

            descendant = null;
            return false;
        }

        private static GameplayEntityView CreateEnemyViewPrefab(
            string name,
            float unitMoveDurationSeconds,
            float entityMoveDurationSeconds)
        {
            var prefabObject = new GameObject(name);
            var view = prefabObject.AddComponent<GameplayEntityView>();
            view.Initialize(20);
            prefabObject.AddComponent<EnemyAnimatorDriver>();
            prefabObject.AddComponent<EnemyAnimationTimingAuthoring>();

            var unitAuthoring = prefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                unitAuthoring,
                "moveMotionDurationSeconds",
                unitMoveDurationSeconds);

            var entityAuthoring = prefabObject.AddComponent<EntityMotionPresentationAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                entityAuthoring,
                "moveMotionDurationSeconds",
                entityMoveDurationSeconds);

            return view;
        }

        private static GameplayEntityView CreateEnemyJumpAnimatorViewPrefab(
            string name,
            out AnimationClip jumpReferenceClip,
            RuntimeAnimatorController runtimeAnimatorController = null)
        {
            var view = CreateEnemyViewPrefab(name, 0.2f, 0.2f);
            var animator = view.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = runtimeAnimatorController != null
                ? runtimeAnimatorController
                : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyJumpAnimatorControllerPath);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null, EnemyJumpAnimatorControllerPath);

            jumpReferenceClip = CreateReferenceClip($"{name}_JumpAirborneReference", 1f);
            var authoring = view.GetComponent<EnemyAnimationTimingAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "stateTransitionCrossFadeDurationSeconds",
                0f);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneAnimatorDurationSeconds",
                1f);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneReferenceClip",
                jumpReferenceClip);
            return view;
        }

        private static AnimatorController CreateJumpAirborneExitTimeAnimatorController(
            string name,
            out AnimationClip jumpClip,
            out AnimationClip moveClip)
        {
            jumpClip = CreateReferenceClip($"{name}_JumpAirborneClip", 1f);
            moveClip = CreateReferenceClip($"{name}_MoveClip", 1f);

            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var moveState = stateMachine.AddState("Move");
            moveState.motion = moveClip;
            var jumpState = stateMachine.AddState("JumpAirborne");
            jumpState.motion = jumpClip;
            stateMachine.defaultState = moveState;

            var exitTransition = jumpState.AddTransition(moveState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 0.75f;
            exitTransition.duration = 0f;
            exitTransition.hasFixedDuration = true;

            var controller = new AnimatorController
            {
                name = $"{name}_Controller",
                layers = new[]
                {
                    new AnimatorControllerLayer
                    {
                        name = "Base Layer",
                        defaultWeight = 1f,
                        stateMachine = stateMachine,
                    },
                },
            };
            controller.AddParameter("JumpAirborne", AnimatorControllerParameterType.Trigger);
            return controller;
        }

        private static AnimationClip CreateReferenceClip(string clipName, float lengthSeconds)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
            };

            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, lengthSeconds, 1f));
            return clip;
        }

        private static Animator GetRequiredAnimator(GameplayEntityView view)
        {
            var animator = view.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            return animator;
        }

        private static void AssertJumpAirborneAnimatorState(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(stateInfo.shortNameHash, Is.EqualTo(Animator.StringToHash("JumpAirborne")));
        }

        private static void AssertJumpAirborneAnimatorStateAndNormalizedTime(
            Animator animator,
            float expectedNormalizedTime)
        {
            AssertJumpAirborneAnimatorState(animator);
            Assert.That(GetAnimatorNormalizedTime(animator), Is.EqualTo(expectedNormalizedTime).Within(0.0001f));
        }

        private static float GetAnimatorNormalizedTime(Animator animator)
        {
            return animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }

        private static float GetRendererFloat(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetFloat(propertyName);
        }

        private static Color GetRendererColor(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor(propertyName);
        }

        private static EnemyInactiveVisualSettings CreateEnemyInactiveVisualSettings(
            Color inactiveTint,
            float desaturateStrength,
            float emissionSuppression,
            float revealInSeconds = 0.25f,
            float revealOutSeconds = 0.18f)
        {
            var settings = ScriptableObject.CreateInstance<EnemyInactiveVisualSettings>();
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveTint", inactiveTint);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "desaturateStrength", desaturateStrength);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "emissionSuppression", emissionSuppression);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveRevealInSeconds", revealInSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveRevealOutSeconds", revealOutSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                settings,
                "inactiveRevealCurve",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return settings;
        }

        private static void AssertColorApproximately(Color expected, Color actual, float tolerance = 0.0001f)
        {
            Assert.That(
                actual.r,
                Is.EqualTo(expected.r).Within(tolerance),
                $"Color.r expected {expected.r:R} but was {actual.r:R}");
            Assert.That(
                actual.g,
                Is.EqualTo(expected.g).Within(tolerance),
                $"Color.g expected {expected.g:R} but was {actual.g:R}");
            Assert.That(
                actual.b,
                Is.EqualTo(expected.b).Within(tolerance),
                $"Color.b expected {expected.b:R} but was {actual.b:R}");
            Assert.That(
                actual.a,
                Is.EqualTo(expected.a).Within(tolerance),
                $"Color.a expected {expected.a:R} but was {actual.a:R}");
        }

        private static Renderer[] ReadDimRenderers(GravityFieldLockedTargetVisualTargetView target)
        {
            var serializedObject = new SerializedObject(target);
            var dimRenderersProperty = serializedObject.FindProperty("dimRenderers");
            Assert.That(dimRenderersProperty, Is.Not.Null);

            var renderers = new Renderer[dimRenderersProperty.arraySize];
            for (var i = 0; i < dimRenderersProperty.arraySize; i++)
            {
                renderers[i] = dimRenderersProperty
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue as Renderer;
            }

            return renderers;
        }

        private sealed class ActiveStateRecordingTileFeatureTarget : MonoBehaviour, ITileFeatureVisualTarget
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public int PlayButtonActivatedCount { get; private set; }

            public bool WasActiveInHierarchyWhenButtonActivated { get; private set; }

            public void Configure(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public void PlayButtonActivated()
            {
                PlayButtonActivatedCount++;
                WasActiveInHierarchyWhenButtonActivated = gameObject.activeInHierarchy;
            }
        }

        private sealed class RecordingGravityFieldVisualTarget :
            MonoBehaviour,
            IGravityFieldActivatedVisualTarget,
            IGravityFieldExpiredVisualTarget,
            IGravityFieldContinuousVisualTarget,
            IGravityFieldLockedTargetVisualTarget,
            IGravityFieldLockedBoxOneShotVisualTarget
        {
            public int ActivatedCount { get; private set; }

            public int ExpiredCount { get; private set; }

            public int ApplyContinuousCount { get; private set; }

            public int ClearContinuousCount { get; private set; }

            public int ApplyLockedTargetCount { get; private set; }

            public int ClearLockedTargetCount { get; private set; }

            public int LockedBoxOneShotCount { get; private set; }

            public GravityFieldVisualState LastContinuousState { get; private set; }

            public int LastLockedTargetEmitterEntityId { get; private set; }

            public GravityFieldLockedBoxPayload LastLockedBoxPayload { get; private set; }

            public void PlayGravityFieldActivated()
            {
                ActivatedCount++;
            }

            public void PlayGravityFieldExpired()
            {
                ExpiredCount++;
            }

            public void ApplyGravityFieldVisualState(GravityFieldVisualState state)
            {
                ApplyContinuousCount++;
                LastContinuousState = state;
            }

            public void ClearGravityFieldVisualState()
            {
                ClearContinuousCount++;
                LastContinuousState = default;
            }

            public void ApplyGravityFieldLockedTarget(int emitterEntityId)
            {
                ApplyLockedTargetCount++;
                LastLockedTargetEmitterEntityId = emitterEntityId;
            }

            public void ClearGravityFieldLockedTarget(int emitterEntityId)
            {
                ClearLockedTargetCount++;
                LastLockedTargetEmitterEntityId = emitterEntityId;
            }

            public void PlayGravityFieldLockedBox(GravityFieldLockedBoxPayload payload)
            {
                LockedBoxOneShotCount++;
                LastLockedBoxPayload = payload;
            }
        }

        private sealed class MotionOverrideViewFactory : IGameplayEntityViewFactory
        {
            private readonly IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> _entityMotionOverridesByEntityId;
            private readonly IReadOnlyDictionary<int, EnemyJumpMotionPresentationSnapshot> _jumpMotionOverridesByEntityId;
            private readonly Transform _parent;
            private readonly IReadOnlyDictionary<int, float> _unitMoveOverridesByEntityId;

            public MotionOverrideViewFactory(
                Transform parent,
                IReadOnlyDictionary<int, float> unitMoveOverridesByEntityId = null,
                IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> entityMotionOverridesByEntityId = null,
                IReadOnlyDictionary<int, EnemyJumpMotionPresentationSnapshot> jumpMotionOverridesByEntityId = null)
            {
                _parent = parent;
                _unitMoveOverridesByEntityId = unitMoveOverridesByEntityId;
                _entityMotionOverridesByEntityId = entityMotionOverridesByEntityId;
                _jumpMotionOverridesByEntityId = jumpMotionOverridesByEntityId;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                viewObject.transform.localPosition = Vector3.zero;
                viewObject.transform.localRotation = Quaternion.identity;
                viewObject.transform.localScale = Vector3.one;

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_unitMoveOverridesByEntityId != null &&
                    _unitMoveOverridesByEntityId.TryGetValue(entity.entityId, out var moveDurationSeconds))
                {
                    var authoring = viewObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", moveDurationSeconds);
                }

                if (_entityMotionOverridesByEntityId != null &&
                    _entityMotionOverridesByEntityId.TryGetValue(entity.entityId, out var entityMotionOverride))
                {
                    var authoring = viewObject.AddComponent<EntityMotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", entityMotionOverride.MoveMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushMotionDurationSeconds", entityMotionOverride.PushMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipMotionDurationSeconds", entityMotionOverride.FlipMotionDurationSeconds);
                }

                if (_jumpMotionOverridesByEntityId != null &&
                    _jumpMotionOverridesByEntityId.TryGetValue(entity.entityId, out var jumpMotionOverride))
                {
                    var authoring = viewObject.AddComponent<EnemyJumpMotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "horizontalHoldBias", jumpMotionOverride.HorizontalHoldBias);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "apexHoldPower", jumpMotionOverride.ApexHoldPower);
                }

                return view;
            }
        }

        private sealed class RecordingEnemySemanticPresentationDriver :
            MonoBehaviour,
            IEnemyVisualSemanticPresentationDriver
        {
            public int ApplyCount { get; private set; }

            public EnemyVisualSemanticState LastState { get; private set; }

            public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
            {
                ApplyCount++;
                LastState = state;
            }
        }
    }
}
