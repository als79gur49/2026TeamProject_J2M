using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxEnemyMotionAttachedFollowerTests
    {
        private const string GlidePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/GlideWindTrailVfx.prefab";
        private const string ChargePrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/ChargeBoosterTrailVfx.prefab";
        private const string GlideBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/GlideWindTrail_Binding.asset";
        private const string ChargeBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ChargeBoosterTrail_Binding.asset";
        private const string BoxSlideFollowBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideFollowLoop_Binding.asset";
        private const string JumperWindupBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperWindupLoop_Binding.asset";
        private const string GlideWindupBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/GlideWindupLoop_Binding.asset";
        private const string GlideRecoverBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/GlideRecoverLoop_Binding.asset";
        private const string EnemyWeaponWindupAuraBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyWeaponWindupAura_Binding.asset";
        private const string EnemyUtilityCooldownAuraBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyUtilityCooldownAura_Binding.asset";
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string DrSaturnPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab";
        private const string BlackEyePrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_BlackEye.prefab";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string FollowerPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyMotionAttachedVfxFollowerPlanner.cs";

        [Test]
        [Category("Extended")]
        public void ChargeActive_AttachesBoosterTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Active) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)));
        }

        [Test]
        [Category("Extended")]
        public void ChargeWindup_DoesNotAttachBoosterTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Windup) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityView_TryGetVfxAttachPoint_FindsNamedAnchor()
        {
            var viewObject = new GameObject("AttachPointView");
            try
            {
                var view = viewObject.AddComponent<GameplayEntityView>();
                var anchor = CreateAttachPoint(view.EnsureModelRoot(), "WeaponAura");

                Assert.That(view.TryGetVfxAttachPoint("WeaponAura", out var point), Is.True);
                Assert.That(point, Is.EqualTo(anchor));
                Assert.That(view.TryGetVfxAttachPoint(" WeaponAura ", out point), Is.True);
                Assert.That(point, Is.EqualTo(anchor));
            }
            finally
            {
                Destroy(viewObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityView_TryGetVfxAttachPoint_RejectsEmptyId()
        {
            var viewObject = new GameObject("AttachPointView");
            try
            {
                var view = viewObject.AddComponent<GameplayEntityView>();
                CreateAttachPoint(view.EnsureModelRoot(), "WeaponAura");

                Assert.That(view.TryGetVfxAttachPoint(null, out _), Is.False);
                Assert.That(view.TryGetVfxAttachPoint(string.Empty, out _), Is.False);
                Assert.That(view.TryGetVfxAttachPoint("   ", out _), Is.False);
            }
            finally
            {
                Destroy(viewObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AttachedFollower_ViewActiveButSemanticInactive_DetachesAndStops()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));

                var visibilityContext = new GameplayVfxVisibilityContext(
                    new Dictionary<int, GameplayVfxEntityVisibilityState>
                    {
                        {
                            40,
                            new GameplayVfxEntityVisibilityState(
                                hasView: true,
                                isViewActiveInHierarchy: true,
                                hasSemanticState: true,
                                isFrontFaceInactive: true)
                        },
                    });
                fixture.Controller.Refresh(
                    11,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false,
                    visibilityContext: visibilityContext,
                    attachedDesiredStates: new[] { DesiredCharge() },
                    attachedFollowersEnabled: true);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Controller.VisibilityBlockedCount, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void FollowVfx_SemanticInactiveThenActive_ReattachesWhenDesiredAgain()
        {
            var fixture = CreateFixture();
            try
            {
                var desired = DesiredCharge();
                var activeContext = new GameplayVfxVisibilityContext(
                    new Dictionary<int, GameplayVfxEntityVisibilityState>
                    {
                        {
                            40,
                            new GameplayVfxEntityVisibilityState(
                                hasView: true,
                                isViewActiveInHierarchy: true,
                                hasSemanticState: true)
                        },
                    });
                var inactiveContext = new GameplayVfxVisibilityContext(
                    new Dictionary<int, GameplayVfxEntityVisibilityState>
                    {
                        {
                            40,
                            new GameplayVfxEntityVisibilityState(
                                hasView: true,
                                isViewActiveInHierarchy: true,
                                hasSemanticState: true,
                                isFrontFaceInactive: true)
                        },
                    });

                fixture.Controller.Refresh(
                    10,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false,
                    visibilityContext: activeContext,
                    attachedDesiredStates: new[] { desired },
                    attachedFollowersEnabled: true);
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                var firstInstance = fixture.View.ModelRoot.GetChild(0);

                fixture.Controller.Refresh(
                    11,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false,
                    visibilityContext: inactiveContext,
                    attachedDesiredStates: new[] { desired },
                    attachedFollowersEnabled: true);
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(firstInstance.parent, Is.EqualTo(fixture.Root.TailRoot));

                fixture.Controller.Refresh(
                    12,
                    fixture.TrackState,
                    fixture.StateStore,
                    fixture.Pool,
                    fixture.BindingResolver,
                    enabled: false,
                    visibilityContext: activeContext,
                    attachedDesiredStates: new[] { desired },
                    attachedFollowersEnabled: true);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0), Is.Not.EqualTo(firstInstance));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayEntityView_TryGetVfxAttachPoint_IgnoresInvalidAttachPoint()
        {
            var viewObject = new GameObject("AttachPointView");
            try
            {
                var view = viewObject.AddComponent<GameplayEntityView>();
                CreateAttachPoint(view.EnsureModelRoot(), " ");

                Assert.That(view.TryGetVfxAttachPoint("WeaponAura", out _), Is.False);
            }
            finally
            {
                Destroy(viewObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void ChargeRecover_DetachesBoosterTrail()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideActive_AttachesWindTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(glideSignals: new[] { CreateGlideSignal(EnemyGlidePhase.Active) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)));
        }

        [Test]
        [Category("Extended")]
        public void GlideWindupAndRecovery_AttachPhaseSpecificLoops()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(glideSignals: new[]
                {
                    CreateGlideSignal(EnemyGlidePhase.Windup, sequence: 10),
                    CreateGlideSignal(EnemyGlidePhase.Recovery, entityId: 42, sequence: 11),
                }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(2));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop)));
            Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyGlideWindup));
            Assert.That(planner.DesiredFollowers[1].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop)));
            Assert.That(planner.DesiredFollowers[1].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyGlideRecover));
        }

        [Test]
        [Category("Extended")]
        public void GlideLandingPendingOrCooldown_DoesNotAttachWindTrail()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(glideSignals: new[]
                {
                    CreateGlideSignal(EnemyGlidePhase.LandingPending),
                    CreateGlideSignal(EnemyGlidePhase.Cooldown, entityId: 42),
                }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BoxSlide_AttachesFollowLoop()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(entityMotions: new[] { CreateBoxSlideMotion() }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop)));
            Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.BoxSlideFollow));
            Assert.That(planner.DesiredFollowers[0].SequenceId, Is.EqualTo(42));
            Assert.That(
                planner.DesiredFollowers[0].RetentionPolicy,
                Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop));
        }

        [Test]
        [Category("Extended")]
        public void BoxSlide_UsesStableFollowerKeyAcrossTicksAndSegments()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(entityMotions: new[] { CreateBoxSlideMotion(40) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);
            var first = planner.DesiredFollowers[0];

            planner.Build(
                tickIndex: 13,
                presentationData: CreatePresentationData(entityMotions: new[]
                {
                    CreateBoxSlideMotion(
                        40,
                        sourceCell: new SurfaceCell(FaceId.Floor, 2, 1),
                        destinationCell: new SurfaceCell(FaceId.Floor, 3, 1)),
                }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);
            var second = planner.DesiredFollowers[0];

            Assert.That(second.Key, Is.EqualTo(first.Key));
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideStartSignal_AttachesFollowLoop()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(boxSlideStartSignals: new[] { CreateBoxSlideStartSignal(40) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop)));
            Assert.That(planner.DesiredFollowers[0].Key, Is.EqualTo(StopKeyForBoxSlide(40)));
            Assert.That(
                planner.DesiredFollowers[0].RetentionPolicy,
                Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop));
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_RemainsSingleModelRootChildAcrossSegments()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.RefreshAttached(DesiredBoxSlide());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0), Is.EqualTo(instance));
                Assert.That(instance.parent, Is.EqualTo(fixture.View.ModelRoot));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_RetainsAcrossIdleRefreshUntilExplicitStop()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0), Is.EqualTo(instance));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_KeepsSameHandleAcrossIdleAndNextStep()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.RefreshAttached();
                fixture.RefreshAttached(DesiredBoxSlide());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0), Is.EqualTo(instance));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_ExplicitStopSignal_Tails()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();
                planner.Build(
                    tickIndex: 13,
                    presentationData: CreatePresentationData(
                        boxSlideStopSignals: new[] { CreateBoxSlideStopSignal(40) }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true);

                fixture.RefreshAttachedWithStops(planner.ExplicitStopKeys);

                Assert.That(planner.ExplicitStopKeys, Has.Count.EqualTo(1));
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_SlidingEndedTick_Tails()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();
                planner.Build(
                    tickIndex: 13,
                    presentationData: CreatePresentationData(),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    finalEntities: new[] { CreateBoxEntityState(40, EntityPhaseState.None) });

                fixture.RefreshAttachedWithStops(planner.ExplicitStopKeys);

                Assert.That(planner.ExplicitStopKeys, Has.Count.EqualTo(1));
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_RemovedExitOrDeathEntity_TailsRetainedFollower()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredBoxSlide());
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();
                planner.Build(
                    tickIndex: 13,
                    presentationData: CreatePresentationData(
                        exitSignals: new[] { CreateExitSignal(40) },
                        visibilityChanges: new[] { CreateRemoveVisibilityChange(40) }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    finalEntities: new[] { CreateBoxEntityState(40, EntityPhaseState.Sliding, hp: 0, markedForDeath: true) });

                fixture.RefreshAttachedWithStops(planner.ExplicitStopKeys);

                Assert.That(planner.ExplicitStopKeys, Has.Count.EqualTo(1));
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideFollow_DetachedEntity_PlansExplicitStop()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 13,
                presentationData: CreatePresentationData(),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true,
                finalEntities: new[]
                {
                    CreateBoxEntityState(
                        40,
                        EntityPhaseState.Sliding,
                        boardPresence: EntityBoardPresence.Detached),
                });

            Assert.That(planner.ExplicitStopKeys, Has.Count.EqualTo(1));
            Assert.That(planner.ExplicitStopKeys[0], Is.EqualTo(StopKeyForBoxSlide()));
        }

        [Test]
        [Category("Extended")]
        public void RefreshDesiredOnlyFollower_MissingDesired_StillTails()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ChargeActiveSignal_MaintainsDesiredAcrossIdleContinuationTick()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Active) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);
            var first = planner.DesiredFollowers[0];

            planner.Build(
                tickIndex: 13,
                presentationData: CreatePresentationData(chargeSignals: new[]
                {
                    CreateChargeSignal(
                        EnemyChargePhase.Active,
                        sequence: 7),
                }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);
            var second = planner.DesiredFollowers[0];

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(second.Key, Is.EqualTo(first.Key));
            Assert.That(second.RetentionPolicy, Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly));
        }

        [Test]
        [Category("Extended")]
        public void GlideWindupLoop_PhaseEnd_Tails()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredGlideWindup());

                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideRecoverLoop_PhaseEnd_Tails()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredGlideRecover());

                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void JumpWindup_AttachesPersistentWindupLoop()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(jumpSignals: new[] { CreateJumpSignal(EnemyJumpPhase.Windup) }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
            Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop)));
            Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyJumpWindup));
        }

        [Test]
        [Category("Extended")]
        public void WeaponWindupAura_PlannerUsesViewAuthoringOptIn()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.View.gameObject.AddComponent<EnemyWeaponAuraVfxAuthoring>();
                CreateAttachPoint(fixture.View.ModelRoot, "WeaponAura");
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(
                        enemyActionSignals: new[] { CreateEnemyActionSignal(startedThisTick: true) }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyWeaponWindupAura: true);

                Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
                Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.WeaponWindupAura)));
                Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyWeaponWindupAura));
                Assert.That(planner.DesiredFollowers[0].AttachPointId, Is.EqualTo("WeaponAura"));
                Assert.That(
                    planner.DesiredFollowers[0].RetentionPolicy,
                    Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void WeaponWindupAura_PlannerIgnoresViewsWithoutAuthoring()
        {
            var fixture = CreateFixture();
            try
            {
                CreateAttachPoint(fixture.View.ModelRoot, "WeaponAura");
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(
                        enemyActionSignals: new[] { CreateEnemyActionSignal(startedThisTick: true) }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyWeaponWindupAura: true);

                Assert.That(planner.DesiredFollowers, Is.Empty);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void UtilityCooldownAura_PlannerUsesViewAuthoringOptIn()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.View.gameObject.AddComponent<EnemyUtilityCooldownAuraVfxAuthoring>();
                CreateAttachPoint(fixture.View.ModelRoot, "WeaponAura");
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(
                        enemyUtilityCooldownSignals: new[] { CreateUtilityCooldownSignal() }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyUtilityCooldownAura: true);

                Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
                Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura)));
                Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyUtilityCooldownAura));
                Assert.That(planner.DesiredFollowers[0].AttachPointId, Is.EqualTo("WeaponAura"));
                Assert.That(
                    planner.DesiredFollowers[0].RetentionPolicy,
                    Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void UtilityCooldownAura_PlannerIgnoresViewsWithoutAuthoring()
        {
            var fixture = CreateFixture();
            try
            {
                CreateAttachPoint(fixture.View.ModelRoot, "WeaponAura");
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(
                        enemyUtilityCooldownSignals: new[] { CreateUtilityCooldownSignal() }),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyUtilityCooldownAura: true);

                Assert.That(planner.DesiredFollowers, Is.Empty);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAttackCooldownFollow_PlannerUsesAuthoritativeCooldownState()
        {
            var fixture = CreateFixture();
            try
            {
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    finalEntities: new[] { CreateEnemyEntityState(40, attackCooldownTicks: 2, attackCooldownTotalTicks: 3) },
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyAttackCooldownFollow: true);

                Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
                Assert.That(planner.DesiredFollowers[0].CueId, Is.EqualTo(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellAttackCooldownFollow)));
                Assert.That(planner.DesiredFollowers[0].StateKind, Is.EqualTo(AttachedVfxFollowerStateKind.EnemyAttackCooldownFollow));
                Assert.That(planner.DesiredFollowers[0].RetentionPolicy, Is.EqualTo(AttachedVfxFollowerRetentionPolicy.RefreshDesiredOnly));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyAttackCooldownFollow_PlannerUsesStatusAuraAttachPointWhenAuthored()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.View.gameObject.AddComponent<EnemyForwardCellProjectileVfxAuthoring>();
                CreateAttachPoint(fixture.View.ModelRoot, "StatusAura");
                var planner = new EnemyMotionAttachedVfxFollowerPlanner();

                planner.Build(
                    tickIndex: 12,
                    presentationData: CreatePresentationData(),
                    enableGlideWindTrail: true,
                    enableChargeBoosterTrail: true,
                    enableBoxSlideFollowLoop: true,
                    enableEnemyJumpWindupLoop: true,
                    finalEntities: new[] { CreateEnemyEntityState(40, attackCooldownTicks: 2, attackCooldownTotalTicks: 3) },
                    viewsByEntityId: fixture.StateStore.ViewsByEntityId,
                    enableEnemyAttackCooldownFollow: true);

                Assert.That(planner.DesiredFollowers, Has.Count.EqualTo(1));
                Assert.That(planner.DesiredFollowers[0].AttachPointId, Is.EqualTo("StatusAura"));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionFollowingVfxController_AttachesFollowerToNamedAttachPoint()
        {
            var fixture = CreateFixture();
            try
            {
                var attachPoint = CreateAttachPoint(fixture.View.ModelRoot, "WeaponAura");

                fixture.RefreshAttached(DesiredWeaponWindupAura());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(attachPoint.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(attachPoint.GetChild(0).parent, Is.EqualTo(attachPoint));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionFollowingVfxController_MissingExplicitAttachPoint_DoesNotFallbackToModelRoot()
        {
            var fixture = CreateFixture();
            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    "VFX attach point 'WeaponAura' not found on entity view 'EnemyView'. Cue='WeaponWindupAura'. The follower will not be spawned.");

                fixture.RefreshAttached(DesiredWeaponWindupAura());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Controller.AttachedMissingOwnerViewCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionFollowingVfxController_EmptyAttachPointId_KeepsExistingModelRootBehavior()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.GetChild(0).parent, Is.EqualTo(fixture.View.ModelRoot));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void AttachedVfxFollowerKey_IncludesAttachPointId()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.WeaponWindupAura);
            var body = new AttachedVfxFollowerKey(
                cueId,
                40,
                AttachedVfxFollowerStateKind.EnemyWeaponWindupAura,
                7);
            var empty = new AttachedVfxFollowerKey(
                cueId,
                40,
                AttachedVfxFollowerStateKind.EnemyWeaponWindupAura,
                7,
                " ");
            var weapon = new AttachedVfxFollowerKey(
                cueId,
                40,
                AttachedVfxFollowerStateKind.EnemyWeaponWindupAura,
                7,
                "WeaponAura");
            var hand = new AttachedVfxFollowerKey(
                cueId,
                40,
                AttachedVfxFollowerStateKind.EnemyWeaponWindupAura,
                7,
                "HandAura");

            Assert.That(empty, Is.EqualTo(body));
            Assert.That(weapon, Is.Not.EqualTo(body));
            Assert.That(hand, Is.Not.EqualTo(weapon));
        }

        [Test]
        [Category("Extended")]
        public void Follower_DoesNotRespawnEveryFrame()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ChargeBoosterTrail_RemainsAttachedAcrossPoolAdvanceWhileActive()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(instance.parent, Is.EqualTo(fixture.View.ModelRoot));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.PoolRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideWindTrail_RemainsAttachedAcrossPoolAdvanceWhileActive()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredGlide());
                var instance = fixture.View.ModelRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(instance.parent, Is.EqualTo(fixture.View.ModelRoot));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.PoolRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_DefaultLifetimeZero_DoesNotImmediateStopWhenControllerManaged()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.TimeProvider.TimeSeconds = 0.01f;
                fixture.Pool.Advance(0.01f);

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Root.TailRoot.childCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_DetachesAndTailsOnStateEnd()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_ReleasesAfterTail()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached();

                fixture.TimeProvider.TimeSeconds = 0.30f;
                fixture.Pool.Advance(0.30f);

                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
                Assert.That(fixture.Root.PoolRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_HardCleanupReleasesAll()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.Controller.HardCleanup();

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_MissingOwnerView_DiagnosticNoMotionSuppression()
        {
            var fixture = CreateFixture(registerView: false);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.AttachedMissingOwnerViewCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void MissingOwner_DetachesExistingFollower()
        {
            var fixture = CreateFixture(tailSeconds: 0.25f);
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                fixture.StateStore.ViewsByEntityId.Remove(40);
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.Zero);
                Assert.That(fixture.View.ModelRoot.childCount, Is.Zero);
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Follower_MissingBinding_DiagnosticNoMotionSuppression()
        {
            var fixture = CreateFixture(resolveBinding: false);
            try
            {
                fixture.RefreshAttached(DesiredCharge());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.AttachedMissingBindingCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOff_DisablesTrailOnly()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge(), DesiredGlide());
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
                Assert.That(fixture.View.ModelRoot.childCount, Is.EqualTo(1));
                Assert.That(fixture.Root.TailRoot.childCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ExitedOrRemovedEntity_DoesNotAttachFollower()
        {
            var planner = new EnemyMotionAttachedVfxFollowerPlanner();
            planner.Build(
                tickIndex: 12,
                presentationData: CreatePresentationData(
                    entityMotions: new[] { CreateBoxSlideMotion(42) },
                    jumpSignals: new[] { CreateJumpSignal(EnemyJumpPhase.Windup, entityId: 43) },
                    chargeSignals: new[] { CreateChargeSignal(EnemyChargePhase.Active) },
                    glideSignals: new[] { CreateGlideSignal(EnemyGlidePhase.Active) },
                    exitSignals: new[] { CreateExitSignal(40) },
                    visibilityChanges: new[]
                    {
                        CreateRemoveVisibilityChange(41),
                        CreateRemoveVisibilityChange(42),
                        CreateRemoveVisibilityChange(43),
                    }),
                enableGlideWindTrail: true,
                enableChargeBoosterTrail: true,
                enableBoxSlideFollowLoop: true,
                enableEnemyJumpWindupLoop: true);

            Assert.That(planner.DesiredFollowers, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void ChargeMotion_NotOwnedByVfxRuntime()
        {
            var runtimeSource = File.ReadAllText(ProductionRuntimePath);
            var plannerSource = File.ReadAllText(FollowerPlannerPath);

            Assert.That(runtimeSource, Does.Not.Contain("ParameterizedMotionVfxCommandBuilder"));
            Assert.That(plannerSource, Does.Not.Contain("ParameterizedMotionVfxCommand"));
            Assert.That(plannerSource, Does.Not.Contain("WorldState"));
            Assert.That(plannerSource, Does.Not.Contain("WorldSnapshot"));
        }

        [Test]
        [Category("Extended")]
        public void GlideMotion_NotOwnedByVfxRuntime()
        {
            var plannerSource = File.ReadAllText(FollowerPlannerPath);

            Assert.That(plannerSource, Does.Contain("EnemyGlidePhase.Active"));
            Assert.That(plannerSource, Does.Not.Contain("MotionTrack"));
            Assert.That(plannerSource, Does.Not.Contain("ParameterizedMotionVfxCommand"));
        }

        [Test]
        [Category("Extended")]
        public void PresentationMotionTrack_FlipImpactStayTrailRegression()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.RefreshAttached(DesiredCharge());

                Assert.That(fixture.Controller.ActiveMotionHandleCount, Is.Zero);
                Assert.That(fixture.Controller.ActiveAttachedHandleCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void RuntimeFlags_DefaultTrue()
        {
            var owner = new GameObject("EnemyMotionAttachedFollowerFlags");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxGlideWindTrail, Is.True);
                Assert.That(runtime.EnableGameplayVfxChargeBoosterTrail, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void GlideWindTrailPrefab_PassesValidation()
        {
            AssertPrefabValid(GlidePrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void ChargeBoosterTrailPrefab_PassesValidation()
        {
            AssertPrefabValid(ChargePrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void GlideWindTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(GlideBindingPath);

            Assert.That(binding, Is.Not.Null, GlideBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.GreaterThan(0f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ChargeBoosterTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(ChargeBindingPath);

            Assert.That(binding, Is.Not.Null, ChargeBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail)));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.TailSeconds, Is.GreaterThan(0f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesBoth()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);
            var composition = GameplayVfxBindingComposition.Compose(cueMap, Array.Empty<VfxProfileAsset>());

            Assert.That(composition.Succeeded, Is.True, string.Join("\n", composition.Validation.Messages));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop));
            AssertResolves(composition.Resolver, GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura));
        }

        [Test]
        [Category("Extended")]
        public void NewPersistentFollowBindings_Validate()
        {
            AssertFollowBinding(BoxSlideFollowBindingPath, GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop));
            AssertFollowBinding(JumperWindupBindingPath, GameplayVfxCueId.From(EnemyVfxCue.JumperWindupLoop));
            AssertFollowBinding(GlideWindupBindingPath, GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop));
            AssertFollowBinding(GlideRecoverBindingPath, GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop));
            AssertFollowBinding(EnemyWeaponWindupAuraBindingPath, GameplayVfxCueId.From(EnemyVfxCue.WeaponWindupAura));
            AssertFollowBinding(EnemyUtilityCooldownAuraBindingPath, GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura));
        }

        [Test]
        [Category("Extended")]
        public void DrSaturnPrefab_HasWeaponAuraAttachPointAndAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrSaturnPrefabPath);

            Assert.That(prefab, Is.Not.Null, DrSaturnPrefabPath);
            Assert.That(prefab.TryGetComponent<EnemyWeaponAuraVfxAuthoring>(out var authoring), Is.True);
            Assert.That(authoring.AttachPointId, Is.EqualTo("WeaponAura"));
            Assert.That(prefab.TryGetComponent<EnemyUtilityCooldownAuraVfxAuthoring>(out var cooldownAuthoring), Is.True);
            Assert.That(cooldownAuthoring.UtilityKind, Is.EqualTo(EnemyUtilityPresentationKind.GravityFieldAura));
            Assert.That(cooldownAuthoring.AttachPointId, Is.EqualTo("WeaponAura"));

            var attachPoints = prefab.GetComponentsInChildren<GameplayVfxAttachPoint>(true)
                .Where(point => point != null && point.Id == "WeaponAura")
                .ToArray();
            Assert.That(attachPoints, Has.Length.EqualTo(1));
            var view = prefab.GetComponent<GameplayEntityView>();
            Assert.That(view.TryGetVfxAttachPoint("WeaponAura", out var point), Is.True);
            Assert.That(point, Is.EqualTo(attachPoints[0].transform));
            Assert.That(point.parent, Is.Not.EqualTo(view.ModelRoot));
            Assert.That(point.parent.name, Is.EqualTo("Blade").Or.EqualTo("R_Hand"));
        }

        [Test]
        [Category("Extended")]
        public void BlackEyePrefab_HasProjectileMuzzleAndStatusAuraAttachPoints()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlackEyePrefabPath);

            Assert.That(prefab, Is.Not.Null, BlackEyePrefabPath);
            Assert.That(prefab.TryGetComponent<EnemyForwardCellProjectileVfxAuthoring>(out var authoring), Is.True);
            Assert.That(authoring.ProjectileMuzzleAttachPointId, Is.EqualTo("ProjectileMuzzle"));
            Assert.That(authoring.AttackCooldownAttachPointId, Is.EqualTo("StatusAura"));

            var attachPoints = prefab.GetComponentsInChildren<GameplayVfxAttachPoint>(true);
            var projectileMuzzle = attachPoints
                .Where(point => point != null && point.Id == "ProjectileMuzzle")
                .ToArray();
            var statusAura = attachPoints
                .Where(point => point != null && point.Id == "StatusAura")
                .ToArray();
            Assert.That(projectileMuzzle, Has.Length.EqualTo(1));
            Assert.That(statusAura, Has.Length.EqualTo(1));

            var view = prefab.GetComponent<GameplayEntityView>();
            Assert.That(view.TryGetVfxAttachPoint("ProjectileMuzzle", out var muzzlePoint), Is.True);
            Assert.That(muzzlePoint, Is.EqualTo(projectileMuzzle[0].transform));
            Assert.That(muzzlePoint.parent.name, Is.EqualTo("Eye"));
            Assert.That(view.TryGetVfxAttachPoint("StatusAura", out var statusPoint), Is.True);
            Assert.That(statusPoint, Is.EqualTo(statusAura[0].transform));
            Assert.That(statusPoint.parent, Is.EqualTo(view.ModelRoot));
        }

        private static TickPresentationData CreatePresentationData(
            IEnumerable<TickEntityMotion> entityMotions = null,
            IEnumerable<TickEnemyJumpPresentationSignal> jumpSignals = null,
            IEnumerable<TickEnemyChargePresentationSignal> chargeSignals = null,
            IEnumerable<TickEnemyGlidePresentationSignal> glideSignals = null,
            IEnumerable<TickEnemyActionPresentationSignal> enemyActionSignals = null,
            IEnumerable<TickEnemyUtilityCooldownPresentationSignal> enemyUtilityCooldownSignals = null,
            IEnumerable<TickEntityExitPresentationSignal> exitSignals = null,
            IEnumerable<TickVisibilityChange> visibilityChanges = null,
            IEnumerable<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IEnumerable<BoxSlideStartPresentationSignal> boxSlideStartSignals = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                null,
                visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals ?? Array.Empty<TickEnemyActionPresentationSignal>(),
                jumpSignals ?? Array.Empty<TickEnemyJumpPresentationSignal>(),
                chargeSignals ?? Array.Empty<TickEnemyChargePresentationSignal>(),
                exitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyGlideSignals: glideSignals ?? Array.Empty<TickEnemyGlidePresentationSignal>(),
                boxSlideStopSignals: boxSlideStopSignals ?? Array.Empty<BoxSlideStopPresentationSignal>(),
                enemyUtilityCooldownSignals: enemyUtilityCooldownSignals ?? Array.Empty<TickEnemyUtilityCooldownPresentationSignal>(),
                boxSlideStartSignals: boxSlideStartSignals ?? Array.Empty<BoxSlideStartPresentationSignal>());
        }

        private static TickEnemyActionPresentationSignal CreateEnemyActionSignal(
            int entityId = 40,
            EnemyActionKind actionKind = EnemyActionKind.Melee,
            int sequence = 17,
            bool startedThisTick = false,
            bool canceledThisTick = false,
            bool executedThisTick = false,
            bool startedRecoveryThisTick = false)
        {
            return new TickEnemyActionPresentationSignal(
                entityId,
                actionKind,
                sequence,
                startedThisTick,
                canceledThisTick,
                executedThisTick,
                startedRecoveryThisTick);
        }

        private static TickEnemyUtilityCooldownPresentationSignal CreateUtilityCooldownSignal(
            int entityId = 40,
            int cooldownTicksRemaining = 5,
            int cooldownTicksTotal = 5,
            int effectIndex = 0,
            int activationSequence = 2)
        {
            return new TickEnemyUtilityCooldownPresentationSignal(
                entityId,
                EnemyUtilityPresentationKind.LockNearbyBoxes,
                cooldownTicksRemaining,
                cooldownTicksTotal,
                effectIndex,
                activationSequence);
        }

        private static TickEnemyChargePresentationSignal CreateChargeSignal(
            EnemyChargePhase phase,
            int entityId = 40,
            int sequence = 7)
        {
            return new TickEnemyChargePresentationSignal(
                entityId,
                sequence,
                phase,
                startedWindupThisTick: phase == EnemyChargePhase.Windup,
                startedActiveThisTick: phase == EnemyChargePhase.Active,
                startedRecoverThisTick: phase == EnemyChargePhase.Recover,
                lockedDirection: Direction.Right);
        }

        private static TickEnemyGlidePresentationSignal CreateGlideSignal(
            EnemyGlidePhase phase,
            int entityId = 41,
            int sequence = 9)
        {
            return new TickEnemyGlidePresentationSignal(
                entityId,
                default,
                phase,
                sequence,
                phaseElapsedTicks: 0,
                phaseTotalTicks: 3,
                normalizedPhaseProgress: 0f,
                liftHeightUnits: 1,
                recoveryDipHeightUnits: 0,
                currentHeightUnits: phase == EnemyGlidePhase.Active ? 1 : 0,
                isAirborneVisual: phase == EnemyGlidePhase.Active,
                isLandingPending: phase == EnemyGlidePhase.LandingPending,
                isTerminalZero: false);
        }

        private static TickEntityMotion CreateBoxSlideMotion(
            int entityId = 42,
            SurfaceCell? sourceCell = null,
            SurfaceCell? destinationCell = null)
        {
            return new TickEntityMotion(
                entityId,
                TickEntityMotionKind.BoxSlide,
                sourceCell ?? new SurfaceCell(FaceId.Floor, 0, 0),
                destinationCell ?? new SurfaceCell(FaceId.Floor, 1, 0));
        }

        private static BoxSlideStartPresentationSignal CreateBoxSlideStartSignal(int entityId = 40)
        {
            return new BoxSlideStartPresentationSignal(
                entityId,
                1,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Floor, 1, 0),
                new CubeTopologyState(FaceId.Floor));
        }

        private static BoxSlideStopPresentationSignal CreateBoxSlideStopSignal(int entityId = 40)
        {
            return new BoxSlideStopPresentationSignal(
                entityId,
                new SurfaceCell(FaceId.Floor, 1, 0),
                new SurfaceCell(FaceId.Floor, 2, 0),
                Direction.Right,
                BoxSlideStopperKind.Terrain,
                0,
                SolidKind.Wall,
                new CubeTopologyState(FaceId.Floor),
                BoxSlideStopCause.SlidingContinuationBlocked);
        }

        private static EntityState CreateBoxEntityState(
            int entityId,
            EntityPhaseState phaseState,
            int hp = 1,
            bool markedForDeath = false,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Box,
                hp = hp,
                maxHp = 1,
                state = phaseState,
                boardPresence = boardPresence,
                position = new SurfaceCell(FaceId.Floor, 1, 0),
                facing = Direction.Right,
            };
        }

        private static EntityState CreateEnemyEntityState(
            int entityId,
            int attackCooldownTicks,
            int attackCooldownTotalTicks)
        {
            return new EntityState
            {
                entityId = entityId,
                type = EntityType.Unit,
                hp = 1,
                maxHp = 1,
                unitRole = UnitRole.Enemy,
                boardPresence = EntityBoardPresence.Occupying,
                position = new SurfaceCell(FaceId.Floor, 1, 0),
                facing = Direction.Right,
                enemyAttackCooldownTicks = attackCooldownTicks,
                enemyAttackCooldownTotalTicks = attackCooldownTotalTicks,
            };
        }

        private static TickEnemyJumpPresentationSignal CreateJumpSignal(
            EnemyJumpPhase phase,
            int entityId = 43,
            int sequence = 11)
        {
            return new TickEnemyJumpPresentationSignal(
                entityId,
                sequence,
                phase,
                startedWindupThisTick: phase == EnemyJumpPhase.Windup,
                startedAirborneThisTick: phase == EnemyJumpPhase.Airborne,
                landedThisTick: false,
                retryThisTick: false,
                sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                lockedTargetCell: new SurfaceCell(FaceId.Floor, 1, 0),
                presentationTargetCell: new SurfaceCell(FaceId.Floor, 1, 0),
                facing: Direction.Right,
                landingTick: 14);
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(int entityId)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                TickEntityExitCause.Killed,
                default,
                default,
                Direction.Up,
                EntityType.Unit);
        }

        private static TickVisibilityChange CreateRemoveVisibilityChange(int entityId)
        {
            return new TickVisibilityChange(
                entityId,
                TickVisibilityChangeKind.Remove,
                default,
                default,
                Direction.Up);
        }

        private static AttachedVfxFollowerDesiredState DesiredCharge()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail),
                40,
                AttachedVfxFollowerStateKind.EnemyChargeActive,
                7,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedVfxFollowerDesiredState DesiredGlide()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail),
                40,
                AttachedVfxFollowerStateKind.EnemyGlideActive,
                9,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedVfxFollowerDesiredState DesiredBoxSlide()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop),
                40,
                AttachedVfxFollowerStateKind.BoxSlideFollow,
                40,
                Vector3.zero,
                Quaternion.identity,
                AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop);
        }

        private static AttachedVfxFollowerDesiredState DesiredGlideWindup()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop),
                40,
                AttachedVfxFollowerStateKind.EnemyGlideWindup,
                9,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedVfxFollowerDesiredState DesiredGlideRecover()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop),
                40,
                AttachedVfxFollowerStateKind.EnemyGlideRecover,
                9,
                Vector3.zero,
                Quaternion.identity);
        }

        private static AttachedVfxFollowerDesiredState DesiredWeaponWindupAura()
        {
            return new AttachedVfxFollowerDesiredState(
                GameplayVfxCueId.From(EnemyVfxCue.WeaponWindupAura),
                40,
                AttachedVfxFollowerStateKind.EnemyWeaponWindupAura,
                17,
                Vector3.zero,
                Quaternion.identity,
                AttachedVfxFollowerRetentionPolicy.RetainUntilExplicitStop,
                "WeaponAura");
        }

        private static AttachedVfxFollowerKey StopKeyForBoxSlide(int entityId = 40)
        {
            return new AttachedVfxFollowerKey(
                GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop),
                entityId,
                AttachedVfxFollowerStateKind.BoxSlideFollow,
                entityId);
        }

        private static AttachedFollowerFixture CreateFixture(
            bool resolveBinding = true,
            bool registerView = true,
            float tailSeconds = 0.25f)
        {
            var owner = new GameObject("EnemyMotionAttachedFollowerFixture");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = GameplayVfxParameterizedMotionRuntimeTests.CreateRuntimePrefab("EnemyMotionAttachedFollowerRuntimePrefab");
            var timeProvider = new GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(
                root,
                new GameplayVfxParameterizedMotionRuntimeTests.SinglePrefabProvider(prefab),
                timeProvider);
            var stateStore = new GameplayPresentationStateStore();
            var viewObject = new GameObject("EnemyView");
            viewObject.transform.SetParent(owner.transform, worldPositionStays: false);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(40);
            view.EnsureModelRoot();
            if (registerView)
            {
                stateStore.ViewsByEntityId[40] = view;
            }

            var policies = new[]
            {
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.ChargeBoosterTrail), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.GlideWindTrail), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.GlideWindupLoop), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.GlideRecoverLoop), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.WeaponWindupAura), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(EnemyVfxCue.UtilityCooldownAura), tailSeconds),
                CreatePolicy(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop), tailSeconds),
            };
            return new AttachedFollowerFixture(
                owner,
                prefab,
                root,
                pool,
                timeProvider,
                stateStore,
                view,
                new GameplayPresentationTrackState(),
                new PresentationMotionFollowingVfxController(),
                new MultiPolicyResolver(resolveBinding, policies));
        }

        private static VfxBindingRuntimePolicy CreatePolicy(GameplayVfxCueId cueId, float tailSeconds)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.Follow,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 8);
        }

        private static void AssertPrefabValid(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.That(prefab, Is.Not.Null, path);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true), Is.Empty);
        }

        private static void AssertFollowBinding(string path, GameplayVfxCueId cueId)
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(path);

            Assert.That(binding, Is.Not.Null, path);
            Assert.That(binding.CueId, Is.EqualTo(cueId));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        private static void AssertResolves(IVfxBindingResolver resolver, GameplayVfxCueId cueId)
        {
            var request = new GameplayVfxRequest(
                1,
                7,
                7,
                sourceEntityId: 40,
                cueId,
                VfxAnchor.ForEntity(40),
                VfxTimingKind.DuringMotion);

            Assert.That(resolver.TryResolve(request, out var policy), Is.True);
            Assert.That(policy.CueId, Is.EqualTo(cueId));
        }

        private static Transform CreateAttachPoint(Transform parent, string id)
        {
            var anchorObject = new GameObject("VfxAttach_" + (string.IsNullOrWhiteSpace(id) ? "Invalid" : id.Trim()));
            anchorObject.transform.SetParent(parent, worldPositionStays: false);
            var attachPoint = anchorObject.AddComponent<GameplayVfxAttachPoint>();
            SetSerializedString(attachPoint, "id", id);
            return anchorObject.transform;
        }

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Destroy(params Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }

        private readonly struct AttachedFollowerFixture
        {
            public AttachedFollowerFixture(
                GameObject owner,
                GameObject prefab,
                GameplayVfxRuntimeRoot root,
                GameplayVfxGameObjectPool pool,
                GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider timeProvider,
                GameplayPresentationStateStore stateStore,
                GameplayEntityView view,
                GameplayPresentationTrackState trackState,
                PresentationMotionFollowingVfxController controller,
                IVfxBindingResolver bindingResolver)
            {
                Owner = owner;
                Prefab = prefab;
                Root = root;
                Pool = pool;
                TimeProvider = timeProvider;
                StateStore = stateStore;
                View = view;
                TrackState = trackState;
                Controller = controller;
                BindingResolver = bindingResolver;
            }

            public GameObject Owner { get; }

            public GameObject Prefab { get; }

            public GameplayVfxRuntimeRoot Root { get; }

            public GameplayVfxGameObjectPool Pool { get; }

            public GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider TimeProvider { get; }

            public GameplayPresentationStateStore StateStore { get; }

            public GameplayEntityView View { get; }

            public GameplayPresentationTrackState TrackState { get; }

            public PresentationMotionFollowingVfxController Controller { get; }

            public IVfxBindingResolver BindingResolver { get; }

            public void RefreshAttached(params AttachedVfxFollowerDesiredState[] desiredStates)
            {
                RefreshAttachedWithStops(Array.Empty<AttachedVfxFollowerKey>(), desiredStates);
            }

            public void RefreshAttachedWithStops(
                IReadOnlyList<AttachedVfxFollowerKey> explicitStopKeys,
                params AttachedVfxFollowerDesiredState[] desiredStates)
            {
                Controller.Refresh(
                    10,
                    TrackState,
                    StateStore,
                    Pool,
                    BindingResolver,
                    enabled: false,
                    attachedDesiredStates: desiredStates,
                    explicitAttachedStopStates: explicitStopKeys,
                    attachedFollowersEnabled: true);
            }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxEnemyMotionAttachedFollowerTests.Destroy(Prefab, Owner);
            }
        }

        private sealed class MultiPolicyResolver : IVfxBindingResolver
        {
            private readonly Dictionary<GameplayVfxCueId, VfxBindingRuntimePolicy> policiesByCueId = new();
            private readonly bool resolve;

            public MultiPolicyResolver(bool resolve, IEnumerable<VfxBindingRuntimePolicy> policies)
            {
                this.resolve = resolve;
                foreach (var policy in policies)
                {
                    policiesByCueId[policy.CueId] = policy;
                }
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                if (resolve && policiesByCueId.TryGetValue(request.CueId, out resolvedPolicy))
                {
                    return true;
                }

                resolvedPolicy = default;
                return false;
            }
        }
    }
}
