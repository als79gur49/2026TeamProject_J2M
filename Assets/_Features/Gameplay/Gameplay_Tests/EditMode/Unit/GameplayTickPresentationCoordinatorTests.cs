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
                var timingProfile = CreateTimingProfile();
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
                Assert.That(modelRoot.localScale.x, Is.LessThan(1f));
                Assert.That(presenter.HasBlockingPresentation, Is.False);

                presenter.UpdatePresentation(timingProfile.MoonBlockEmergenceDurationSeconds + 0.01f);

                AssertPositionApproximately(view.transform.localPosition, rootPositionDuringEmergence);
                Assert.That(view.transform.localRotation, Is.EqualTo(rootRotationDuringEmergence));
                AssertPositionApproximately(modelRoot.localPosition, Vector3.zero);
                AssertPositionApproximately(modelRoot.localScale, Vector3.one);
                Assert.That(view.GetComponent<MoonBlockEmergencePresentationDriver>().IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_UsesNonLinearDotweenEase()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_UsesNonLinearDotweenEase));
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
                var lockDurationSeconds =
                    MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks /
                    (float)timingProfile.SimulationTicksPerSecond;
                presenter.UpdatePresentation(lockDurationSeconds * 0.5f);

                Assert.That(view.ModelRoot.localScale.x, Is.Not.EqualTo(0.6f).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_DurationDoesNotExceedSpawnLockWindow()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_DurationDoesNotExceedSpawnLockWindow));
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

                presenter.UpdatePresentation(lockDurationSeconds + 0.01f);

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
        public void GlidePresentation_ComposesWithLegacyActiveMove()
        {
            var rootObject = new GameObject("GlidePresentation_ComposesWithLegacyActiveMove");

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
                Assert.That(Vector3.Distance(view.transform.localPosition, poseBeforeLanding), Is.GreaterThan(0.001f));
                Assert.That(Vector3.Distance(view.transform.localPosition, landingPosition), Is.GreaterThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

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
            Assert.That(gravityFieldTargetView, Does.Not.Contain("sharedMaterial"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("sharedMaterial"));

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
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    spawnCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
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
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.IsTransitionOnlyVisible, Is.False);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Front));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
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
                isLandingPending: phase == EnemyGlidePhase.LandingPending,
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
            private readonly Transform _parent;
            private readonly IReadOnlyDictionary<int, float> _unitMoveOverridesByEntityId;

            public MotionOverrideViewFactory(
                Transform parent,
                IReadOnlyDictionary<int, float> unitMoveOverridesByEntityId = null,
                IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> entityMotionOverridesByEntityId = null)
            {
                _parent = parent;
                _unitMoveOverridesByEntityId = unitMoveOverridesByEntityId;
                _entityMotionOverridesByEntityId = entityMotionOverridesByEntityId;
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

                return view;
            }
        }
    }
}
