using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileFeatureVisualHandlerIsolationTests
    {
        [Test]
        [Category("Extended")]
        public void FeatureSpecificHandlers_OnlyHandleTheirOwnCueFamilies()
        {
            AssertHandlerCues(
                new ButtonTileFeatureVisualHandler(),
                TileFeatureVisualCueId.ButtonActivated,
                TileFeatureVisualCueId.ButtonVisibleLoop,
                TileFeatureVisualCueId.ButtonActiveLoop);
            AssertHandlerCues(
                new DestroyTileFeatureVisualHandler(),
                TileFeatureVisualCueId.DestroyTileTriggered,
                TileFeatureVisualCueId.DestroyTileActivated,
                TileFeatureVisualCueId.DestroyTileDeactivated,
                TileFeatureVisualCueId.DestroyTileActiveState,
                TileFeatureVisualCueId.DestroyTileLaserActive);
            AssertHandlerCues(
                new SlideTileFeatureVisualHandler(),
                TileFeatureVisualCueId.SlideTileRedirected,
                TileFeatureVisualCueId.SlideTileActiveState);
            AssertHandlerCues(
                new BarricadeTileFeatureVisualHandler(),
                TileFeatureVisualCueId.BarricadeBlocked,
                TileFeatureVisualCueId.BarricadeCrushed,
                TileFeatureVisualCueId.BarricadeActivated,
                TileFeatureVisualCueId.BarricadeDeactivated,
                TileFeatureVisualCueId.BarricadeActiveState,
                TileFeatureVisualCueId.BarricadeActiveLoop);
            AssertHandlerCues(
                new ExitTileFeatureVisualHandler(),
                TileFeatureVisualCueId.ExitOpened,
                TileFeatureVisualCueId.ExitEntered,
                TileFeatureVisualCueId.ExitOpenState,
                TileFeatureVisualCueId.ExitOpenLoop,
                TileFeatureVisualCueId.EntranceSpawn);
            AssertHandlerCues(
                new MoonBlockGeneratorTileFeatureVisualHandler(),
                TileFeatureVisualCueId.MoonBlockGenerated,
                TileFeatureVisualCueId.MoonBlockGeneratorBlocked);
        }

        [Test]
        [Category("Extended")]
        public void BarricadeProfileHandler_SameActiveRefresh_DoesNotReplayIdleState()
        {
            var targetObject = new GameObject(nameof(BarricadeProfileHandler_SameActiveRefresh_DoesNotReplayIdleState));
            var animatorController = CreateBarricadeAnimatorController(nameof(BarricadeProfileHandler_SameActiveRefresh_DoesNotReplayIdleState));
            var profile = CreateBarricadeProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new BarricadeTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Front, 1, 1);
                var activeStateRequest = new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.BarricadeActiveState,
                    100,
                    cell,
                    TileFeatureKind.Barricade,
                    active: true);

                handler.TryHandle(activeStateRequest, null, profile, animator, null);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("RaisedIdle")));

                animator.Play(Animator.StringToHash("BlockedPulse"), 0, 0f);
                animator.Update(0f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("BlockedPulse")));

                handler.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.BarricadeBlocked,
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        direction: Direction.Right,
                        targetEntityId: 20,
                        active: true),
                    null,
                    profile,
                    animator,
                    null);
                handler.TryHandle(activeStateRequest, null, profile, animator, null);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("BlockedPulse")));
                Assert.That(animator.GetBool("BarricadeActive"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void BarricadeProfileHandler_ChangedActiveState_PlaysBoundIdleState()
        {
            var targetObject = new GameObject(nameof(BarricadeProfileHandler_ChangedActiveState_PlaysBoundIdleState));
            var animatorController = CreateBarricadeAnimatorController(nameof(BarricadeProfileHandler_ChangedActiveState_PlaysBoundIdleState));
            var profile = CreateBarricadeProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new BarricadeTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Front, 1, 1);

                handler.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.BarricadeActiveState,
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        active: true),
                    null,
                    profile,
                    animator,
                    null);
                handler.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.BarricadeActiveState,
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        active: false),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("LoweredIdle")));
                Assert.That(animator.GetBool("BarricadeActive"), Is.False);

                handler.TryHandle(
                    new TileFeatureVisualRequest(
                        TileFeatureVisualCueId.BarricadeActiveState,
                        100,
                        cell,
                        TileFeatureKind.Barricade,
                        active: true),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("RaisedIdle")));
                Assert.That(animator.GetBool("BarricadeActive"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitOpened_WithOpenStateRefresh_DoesNotReplayClosedOrOpenIdleUnexpectedly()
        {
            var targetObject = new GameObject(nameof(ExitOpened_WithOpenStateRefresh_DoesNotReplayClosedOrOpenIdleUnexpectedly));
            var animatorController = CreateExitAnimatorController(nameof(ExitOpened_WithOpenStateRefresh_DoesNotReplayClosedOrOpenIdleUnexpectedly));
            var profile = CreateExitProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new ExitTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var pulseHash = Animator.StringToHash("OpenPulse");

                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpened, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);
                animator.Play(pulseHash, 0, 0f);
                animator.Update(0f);
                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(handler.DebugOpenStateAnimatorStatePlayCount, Is.Zero);
                Assert.That(animator.GetBool("ExitOpen"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(pulseHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitEntered_WithOpenStateRefresh_DoesNotReplayIdleUnexpectedly()
        {
            var targetObject = new GameObject(nameof(ExitEntered_WithOpenStateRefresh_DoesNotReplayIdleUnexpectedly));
            var animatorController = CreateExitAnimatorController(nameof(ExitEntered_WithOpenStateRefresh_DoesNotReplayIdleUnexpectedly));
            var profile = CreateExitProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new ExitTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var pulseHash = Animator.StringToHash("EnteredPulse");

                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);
                animator.Play(pulseHash, 0, 0f);
                animator.Update(0f);
                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitEntered, 100, cell, TileFeatureKind.Exit, targetEntityId: 20),
                    null,
                    profile,
                    animator,
                    null);
                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(handler.DebugOpenStateAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(animator.GetBool("ExitOpen"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(pulseHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitOpenState_ChangedState_PlaysBoundState()
        {
            var targetObject = new GameObject(nameof(ExitOpenState_ChangedState_PlaysBoundState));
            var animatorController = CreateExitAnimatorController(nameof(ExitOpenState_ChangedState_PlaysBoundState));
            var profile = CreateExitProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new ExitTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);

                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(handler.DebugOpenStateAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(animator.GetBool("ExitOpen"), Is.True);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("ExitOpenedIdle")));

                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: false),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(handler.DebugOpenStateAnimatorStatePlayCount, Is.EqualTo(2));
                Assert.That(animator.GetBool("ExitOpen"), Is.False);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    Is.EqualTo(Animator.StringToHash("ExitClosedIdle")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitOpenState_SameState_DoesNotClobberOneShotTrigger()
        {
            var targetObject = new GameObject(nameof(ExitOpenState_SameState_DoesNotClobberOneShotTrigger));
            var animatorController = CreateExitAnimatorController(nameof(ExitOpenState_SameState_DoesNotClobberOneShotTrigger));
            var profile = CreateExitProfile();

            try
            {
                var animator = targetObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = animatorController;
                var handler = new ExitTileFeatureVisualHandler();
                var cell = new SurfaceCell(FaceId.Floor, 1, 1);
                var pulseHash = Animator.StringToHash("OpenPulse");

                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);
                animator.Play(pulseHash, 0, 0f);
                animator.Update(0f);
                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpened, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);
                handler.TryHandle(
                    new TileFeatureVisualRequest(TileFeatureVisualCueId.ExitOpenState, 100, cell, TileFeatureKind.Exit, active: true),
                    null,
                    profile,
                    animator,
                    null);

                Assert.That(handler.DebugOpenStateAnimatorStatePlayCount, Is.EqualTo(1));
                Assert.That(animator.GetBool("ExitOpen"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(pulseHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(animatorController);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        private static void AssertHandlerCues(
            ITileFeatureVisualHandler handler,
            params TileFeatureVisualCueId[] expectedCues)
        {
            var expected = new HashSet<TileFeatureVisualCueId>(expectedCues);
            foreach (TileFeatureVisualCueId cueId in Enum.GetValues(typeof(TileFeatureVisualCueId)))
            {
                if (cueId == TileFeatureVisualCueId.None)
                {
                    continue;
                }

                Assert.That(handler.CanHandle(cueId), Is.EqualTo(expected.Contains(cueId)), $"{handler.GetType().Name}:{cueId}");
            }
        }

        private static TileFeatureVisualProfile CreateBarricadeProfile()
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            SetPrivateField(profile, "featureKind", TileFeatureKind.Barricade);
            SetPrivateField(
                profile,
                "cueBindings",
                new[]
                {
                    CreateBinding(
                        TileFeatureVisualCueId.BarricadeBlocked,
                        TileFeatureAnimatorBindingKind.Trigger,
                        "BarricadeBlocked"),
                    CreateBinding(
                        TileFeatureVisualCueId.BarricadeActiveState,
                        TileFeatureAnimatorBindingKind.Bool,
                        "BarricadeActive",
                        activeStateName: "RaisedIdle",
                        inactiveStateName: "LoweredIdle"),
                });
            return profile;
        }

        private static TileFeatureVisualProfile CreateExitProfile()
        {
            var profile = ScriptableObject.CreateInstance<TileFeatureVisualProfile>();
            SetPrivateField(profile, "featureKind", TileFeatureKind.Exit);
            SetPrivateField(
                profile,
                "cueBindings",
                new[]
                {
                    CreateBinding(
                        TileFeatureVisualCueId.ExitOpened,
                        TileFeatureAnimatorBindingKind.Trigger,
                        "ExitOpened"),
                    CreateBinding(
                        TileFeatureVisualCueId.ExitEntered,
                        TileFeatureAnimatorBindingKind.Trigger,
                        "ExitEntered"),
                    CreateBinding(
                        TileFeatureVisualCueId.ExitOpenState,
                        TileFeatureAnimatorBindingKind.Bool,
                        "ExitOpen",
                        activeStateName: "ExitOpenedIdle",
                        inactiveStateName: "ExitClosedIdle"),
                });
            return profile;
        }

        private static TileFeatureVisualCueBinding CreateBinding(
            TileFeatureVisualCueId cueId,
            TileFeatureAnimatorBindingKind kind,
            string parameterOrStateName,
            string activeStateName = null,
            string inactiveStateName = null)
        {
            return new TileFeatureVisualCueBinding
            {
                CueId = cueId,
                TargetSlot = TileFeatureVisualSlotId.Root,
                AnimatorBinding = new TileFeatureAnimatorBinding
                {
                    CueId = cueId,
                    Kind = kind,
                    ParameterOrStateName = parameterOrStateName,
                },
                ActiveStateAnimatorBinding = string.IsNullOrWhiteSpace(activeStateName)
                    ? default
                    : new TileFeatureAnimatorBinding
                    {
                        CueId = cueId,
                        Kind = TileFeatureAnimatorBindingKind.State,
                        ParameterOrStateName = activeStateName,
                    },
                InactiveStateAnimatorBinding = string.IsNullOrWhiteSpace(inactiveStateName)
                    ? default
                    : new TileFeatureAnimatorBinding
                    {
                        CueId = cueId,
                        Kind = TileFeatureAnimatorBindingKind.State,
                        ParameterOrStateName = inactiveStateName,
                    },
            };
        }

        private static AnimatorController CreateBarricadeAnimatorController(string name)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var loweredState = stateMachine.AddState("LoweredIdle");
            stateMachine.AddState("RaisedIdle");
            stateMachine.AddState("BlockedPulse");
            stateMachine.defaultState = loweredState;

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
            controller.AddParameter("BarricadeBlocked", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("BarricadeActive", AnimatorControllerParameterType.Bool);
            return controller;
        }

        private static AnimatorController CreateExitAnimatorController(string name)
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var closedState = stateMachine.AddState("ExitClosedIdle");
            stateMachine.AddState("ExitOpenedIdle");
            stateMachine.AddState("OpenPulse");
            stateMachine.AddState("EnteredPulse");
            stateMachine.defaultState = closedState;

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
            controller.AddParameter("ExitOpened", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ExitEntered", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ExitOpen", AnimatorControllerParameterType.Bool);
            return controller;
        }

        private static void SetPrivateField<T>(TileFeatureVisualProfile profile, string fieldName, T value)
        {
            typeof(TileFeatureVisualProfile)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, value);
        }
    }
}
