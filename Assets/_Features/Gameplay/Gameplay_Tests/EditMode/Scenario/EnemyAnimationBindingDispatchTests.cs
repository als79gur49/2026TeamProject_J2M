using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    [Category("Full")]
    public sealed class EnemyAnimationBindingDispatchTests
    {
        [Test]
        public void NewBinding_IsExclusive_AndMissingCueDoesNotUseLegacyFallback()
        {
            using var fixture = Fixture.Create(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "Fire"));
            SetField(fixture.Driver, "attackTriggerName", "LegacyFire");

            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionExecute),
                Is.EqualTo(EnemyAnimationDispatchResult.Applied));
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "FireTrap");
            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.Hit),
                Is.EqualTo(EnemyAnimationDispatchResult.Unsupported));
        }

        [Test]
        public void StatePending_IsOneSlotLastWriteWins_AndCapturesSnapshotCrossFade()
        {
            using var fixture = Fixture.Create(
                0.2f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateA"),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateB"));
            fixture.Animator.enabled = false;

            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionWindup),
                Is.EqualTo(EnemyAnimationDispatchResult.Queued));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateA"));
            fixture.Authoring.ConfigureForTests(
                new[]
                {
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.ActionRecovery,
                        EnemyAnimationDispatchMode.State,
                        "StateB"),
                },
                0.8f);
            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionRecovery),
                Is.EqualTo(EnemyAnimationDispatchResult.Queued));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateB"));
            Assert.That(fixture.Driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.2f));

            fixture.Animator.enabled = true;
            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.25f);
            AssertState(fixture.Animator, "StateB");
            Assert.That(fixture.Driver.LastCrossFadeDurationSeconds, Is.EqualTo(0.2f));

            fixture.Animator.Play("Move", 0, 0f);
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.25f);
            AssertState(fixture.Animator, "Move", "The pending state command must be consumed once.");
        }

        [Test]
        public void TriggerUnavailable_IsNotQueued()
        {
            using var fixture = Fixture.Create(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "Fire"));
            fixture.Animator.enabled = false;

            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionExecute),
                Is.EqualTo(EnemyAnimationDispatchResult.AnimatorUnavailable));

            fixture.Animator.enabled = true;
            fixture.Animator.Rebind();
            fixture.Animator.Update(0f);
            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "Move");
        }

        [Test]
        public void TriggerMissingOrWrongType_ReturnsAnimatorUnavailableWithoutLateDispatch()
        {
            using var missingFixture = Fixture.Create(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "MissingTrigger"));

            Assert.That(
                missingFixture.Driver.DispatchCue(EnemyAnimationCue.ActionExecute),
                Is.EqualTo(EnemyAnimationDispatchResult.AnimatorUnavailable));
            missingFixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            missingFixture.Animator.Update(0.02f);
            AssertState(missingFixture.Animator, "Move");

            using var wrongTypeFixture = Fixture.Create(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "WrongType"));
            wrongTypeFixture.Controller.AddParameter("WrongType", AnimatorControllerParameterType.Bool);
            wrongTypeFixture.Rebind();

            Assert.That(
                wrongTypeFixture.Driver.DispatchCue(EnemyAnimationCue.ActionExecute),
                Is.EqualTo(EnemyAnimationDispatchResult.AnimatorUnavailable));
            wrongTypeFixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            wrongTypeFixture.Animator.Update(0.02f);
            AssertState(wrongTypeFixture.Animator, "Move");
        }

        [Test]
        public void LiveStateDispatch_InvalidatesDifferentOlderPendingState()
        {
            using var fixture = Fixture.Create(
                0f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateA"),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateB"));
            fixture.Animator.enabled = false;
            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionWindup),
                Is.EqualTo(EnemyAnimationDispatchResult.Queued));

            fixture.Animator.enabled = true;
            fixture.Rebind();
            Assert.That(
                fixture.Driver.DispatchCue(EnemyAnimationCue.ActionRecovery),
                Is.EqualTo(EnemyAnimationDispatchResult.Applied));
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "StateB");

            fixture.Driver.SyncRuntimeState(isVisible: true, isMoving: false);
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "StateB", "A successful live state command must invalidate older pending state.");
        }

        [Test]
        public void WindupFamily_SelectsExactlyOneCueBySemanticPriority()
        {
            using var glideFixture = CreateWindupPriorityFixture();
            glideFixture.Driver.Apply(WindupPriorityState(
                glide: true,
                charge: true,
                utility: true,
                action: true));
            Assert.That(glideFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateA"));
            Assert.That(
                glideFixture.Driver.ResolveActiveTimingCue(glideFixture.Driver.LastPresentationState),
                Is.EqualTo(EnemyAnimationCue.GlideWindup));
            Assert.That(
                glideFixture.Driver.GetPresentationDurationSeconds(
                    EnemyAnimatorDriver.EnemyPresentationPhase.Windup),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(glideFixture.Animator.speed, Is.EqualTo(4f).Within(0.0001f));

            using var chargeFixture = CreateWindupPriorityFixture();
            chargeFixture.Driver.Apply(WindupPriorityState(
                glide: false,
                charge: true,
                utility: true,
                action: true));
            Assert.That(chargeFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateB"));

            using var utilityFixture = CreateWindupPriorityFixture();
            utilityFixture.Driver.Apply(WindupPriorityState(
                glide: false,
                charge: false,
                utility: true,
                action: true));
            Assert.That(utilityFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateC"));
        }

        [Test]
        public void RecoveryFamily_SelectsChargeAndUtilityBeforeSummonNoVisualBranch()
        {
            using var glideFixture = CreateRecoveryPriorityFixture();
            glideFixture.Driver.Apply(RecoveryPriorityState(
                charge: true,
                utility: true,
                summon: true,
                glide: true));
            Assert.That(glideFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateA"));
            Assert.That(
                glideFixture.Driver.ResolveActiveTimingCue(glideFixture.Driver.LastPresentationState),
                Is.EqualTo(EnemyAnimationCue.GlideRecovery));
            Assert.That(
                glideFixture.Driver.GetPresentationDurationSeconds(
                    EnemyAnimatorDriver.EnemyPresentationPhase.Recovery),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(glideFixture.Animator.speed, Is.EqualTo(4f).Within(0.0001f));

            using var chargeFixture = CreateRecoveryPriorityFixture();
            chargeFixture.Driver.Apply(RecoveryPriorityState(charge: true, utility: true, summon: true));
            Assert.That(chargeFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateB"));

            using var utilityFixture = CreateRecoveryPriorityFixture();
            utilityFixture.Driver.Apply(RecoveryPriorityState(charge: false, utility: true, summon: true));
            Assert.That(utilityFixture.Driver.LastCrossFadedStateName, Is.EqualTo("StateC"));

            using var summonFixture = CreateRecoveryPriorityFixture();
            summonFixture.Driver.Apply(RecoveryPriorityState(charge: false, utility: false, summon: true));
            Assert.That(summonFixture.Driver.LastCrossFadedStateName, Is.Empty);
            Assert.That(summonFixture.Driver.RecoverySignalCount, Is.EqualTo(1));
        }

        [Test]
        public void NewStateBinding_UnresolvableLayerZeroStateFailsFast()
        {
            using var fixture = Fixture.Create(
                0f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "Missing"));

            Assert.Throws<InvalidOperationException>(
                () => fixture.Driver.DispatchCue(EnemyAnimationCue.ActionWindup));
        }

        [Test]
        public void JumpAirborneTrigger_UsesSustainedStateForResyncWithoutNewSignal()
        {
            using var fixture = Fixture.Create(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.Trigger,
                    "JumpStart",
                    sustainedStateName: "JumpLoop"));

            fixture.Driver.Apply(JumpAirborneState());
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "JumpTrap");
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));

            Assert.That(fixture.Driver.ResyncAnimatorStateFromLastPresentation(), Is.True);
            fixture.Animator.Update(0.02f);
            AssertState(fixture.Animator, "JumpLoop");
            Assert.That(fixture.Driver.JumpAirborneSignalCount, Is.EqualTo(1));
        }

        [Test]
        public void SummonRecovery_IncrementsCounterWithoutActionRecoveryDispatch()
        {
            using var fixture = Fixture.Create(
                0f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateB"));

            fixture.Driver.Apply(SummonRecoveryState());
            fixture.Animator.Update(0.02f);

            Assert.That(fixture.Driver.RecoverySignalCount, Is.EqualTo(1));
            Assert.That(fixture.Driver.LastCrossFadedStateName, Is.Empty);
            AssertState(fixture.Animator, "Move");
        }

        [Test]
        public void NewTiming_IgnoresContradictoryLegacyTiming_AndPhaseAdapterRejectsAmbiguity()
        {
            using var fixture = Fixture.Create(
                -1f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Fire",
                    animatorDurationSeconds: 0.5f,
                    referenceClip: null),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.UtilityWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "LegacyFire",
                    animatorDurationSeconds: 0.25f,
                    referenceClip: null));
            fixture.ReplaceBindingReferenceClips();
            SetField(fixture.LegacyTiming, "stateTransitionCrossFadeDurationSeconds", float.NaN);

            Assert.That(
                fixture.Driver.GetPresentationDurationSeconds(EnemyAnimationCue.ActionWindup),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.Throws<InvalidOperationException>(
                () => fixture.Driver.GetPresentationDurationSeconds(
                    EnemyAnimatorDriver.EnemyPresentationPhase.Windup));
        }

        private static EnemyViewPresentationState JumpAirborneState()
        {
            return new EnemyViewPresentationState(
                entityId: 1,
                tickIndex: 1,
                EnemyAiMode.Patrol,
                EnemyActionKind.None,
                EnemyJumpPhase.Airborne,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: true,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static EnemyViewPresentationState SummonRecoveryState()
        {
            return new EnemyViewPresentationState(
                entityId: 1,
                tickIndex: 1,
                EnemyAiMode.Recover,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: true,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false,
                startedSummonRecoverThisTick: true);
        }

        private static Fixture CreateWindupPriorityFixture()
        {
            var fixture = Fixture.Create(
                0f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.GlideWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateA",
                    animatorDurationSeconds: 0.25f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ChargeWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateB",
                    animatorDurationSeconds: 0.75f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.UtilityWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateC",
                    animatorDurationSeconds: 0.5f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "StateD",
                    animatorDurationSeconds: 1f));
            fixture.ReplaceBindingReferenceClips();
            return fixture;
        }

        private static Fixture CreateRecoveryPriorityFixture()
        {
            var fixture = Fixture.Create(
                0f,
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.GlideRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateA",
                    animatorDurationSeconds: 0.25f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ChargeRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateB",
                    animatorDurationSeconds: 0.75f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.UtilityRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateC",
                    animatorDurationSeconds: 0.5f),
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionRecovery,
                    EnemyAnimationDispatchMode.State,
                    "StateD",
                    animatorDurationSeconds: 1f));
            fixture.ReplaceBindingReferenceClips();
            return fixture;
        }

        private static EnemyViewPresentationState WindupPriorityState(
            bool glide,
            bool charge,
            bool utility,
            bool action)
        {
            return new EnemyViewPresentationState(
                entityId: 1,
                tickIndex: 1,
                EnemyAiMode.Attack,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                charge ? EnemyChargePhase.Windup : EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: action || glide || charge,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: charge,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: false,
                tookDamage: false,
                didDie: false,
                glidePhase: glide ? EnemyGlidePhase.Windup : EnemyGlidePhase.Ready,
                startedGlideWindupThisTick: glide,
                utilityPresentationKind: utility
                    ? EnemyUtilityPresentationKind.GravityFieldAura
                    : EnemyUtilityPresentationKind.None,
                startedUtilityWindupThisTick: utility);
        }

        private static EnemyViewPresentationState RecoveryPriorityState(
            bool charge,
            bool utility,
            bool summon,
            bool glide = false)
        {
            return new EnemyViewPresentationState(
                entityId: 1,
                tickIndex: 1,
                EnemyAiMode.Recover,
                EnemyActionKind.None,
                EnemyJumpPhase.None,
                charge ? EnemyChargePhase.Recover : EnemyChargePhase.None,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: true,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: false,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                startedChargeWindupThisTick: false,
                startedChargeActiveThisTick: false,
                startedChargeRecoverThisTick: charge,
                tookDamage: false,
                didDie: false,
                glidePhase: glide ? EnemyGlidePhase.Recovery : EnemyGlidePhase.Ready,
                startedGlideRecoverThisTick: glide,
                utilityPresentationKind: utility
                    ? EnemyUtilityPresentationKind.GravityFieldAura
                    : EnemyUtilityPresentationKind.None,
                startedUtilityRecoverThisTick: utility,
                startedSummonRecoverThisTick: summon);
        }

        private static void AssertState(Animator animator, string stateName, string message = null)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(
                state.IsName(stateName) || state.IsName("Base Layer." + stateName),
                Is.True,
                message ?? $"Expected '{stateName}', actual short hash {state.shortNameHash}.");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> _owned = new();
            private EnemyAnimationCueBinding[] _sourceBindings;
            private float _crossFade;

            private Fixture(GameObject root)
            {
                Root = root;
            }

            public GameObject Root { get; }
            public Animator Animator { get; private set; }
            public AnimatorController Controller { get; private set; }
            public EnemyAnimatorDriver Driver { get; private set; }
            public EnemyAnimationBindingAuthoring Authoring { get; private set; }
            public EnemyAnimationTimingAuthoring LegacyTiming { get; private set; }
            public AnimationClip ReferenceClip { get; private set; }

            public static Fixture Create(params EnemyAnimationCueBinding[] bindings)
            {
                return Create(-1f, bindings);
            }

            public static Fixture Create(float crossFade, params EnemyAnimationCueBinding[] bindings)
            {
                var fixture = new Fixture(new GameObject(nameof(EnemyAnimationBindingDispatchTests)));
                fixture.Build(crossFade, bindings);
                return fixture;
            }

            public void ReplaceBindingReferenceClips()
            {
                for (var index = 0; index < _sourceBindings.Length; index++)
                {
                    var binding = _sourceBindings[index];
                    _sourceBindings[index] = EnemyAnimationCueBinding.CreateForTests(
                        binding.Cue,
                        binding.PrimaryDispatchMode,
                        binding.TargetName,
                        binding.SustainedStateName,
                        binding.AnimatorDurationSeconds,
                        ReferenceClip);
                }

                Authoring.ConfigureForTests(_sourceBindings, _crossFade);
            }

            public void Rebind()
            {
                Animator.Rebind();
                Animator.Update(0f);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                for (var index = _owned.Count - 1; index >= 0; index--)
                {
                    if (_owned[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_owned[index]);
                    }
                }
            }

            private void Build(float crossFade, EnemyAnimationCueBinding[] bindings)
            {
                var animatorObject = new GameObject("Animator");
                animatorObject.transform.SetParent(Root.transform, false);
                Animator = animatorObject.AddComponent<Animator>();
                Controller = CreateController();
                Animator.runtimeAnimatorController = Controller;
                LegacyTiming = Root.AddComponent<EnemyAnimationTimingAuthoring>();
                Authoring = Root.AddComponent<EnemyAnimationBindingAuthoring>();
                _sourceBindings = (EnemyAnimationCueBinding[])bindings.Clone();
                _crossFade = crossFade;
                Authoring.ConfigureForTests(_sourceBindings, crossFade);
                Driver = Root.AddComponent<EnemyAnimatorDriver>();
                SetField(Driver, "animator", Animator);
                SetField(Driver, "animationTimingAuthoring", LegacyTiming);
                Rebind();
            }

            private AnimatorController CreateController()
            {
                var machine = new AnimatorStateMachine { hideFlags = HideFlags.HideAndDontSave };
                _owned.Add(machine);
                var states = new Dictionary<string, AnimatorState>();
                foreach (var stateName in new[]
                         {
                             "Move", "StateA", "StateB", "StateC", "StateD", "JumpLoop", "FireTrap",
                             "LegacyFireTrap", "JumpTrap",
                         })
                {
                    var state = machine.AddState(stateName);
                    var clip = CreateClip(stateName);
                    state.motion = clip;
                    states.Add(stateName, state);
                }

                machine.defaultState = states["Move"];
                var controller = new AnimatorController
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layers = new[]
                    {
                        new AnimatorControllerLayer
                        {
                            name = "Base Layer",
                            defaultWeight = 1f,
                            stateMachine = machine,
                        },
                    },
                };
                _owned.Add(controller);
                AddTrigger(controller, machine, states["FireTrap"], "Fire");
                AddTrigger(controller, machine, states["LegacyFireTrap"], "LegacyFire");
                AddTrigger(controller, machine, states["JumpTrap"], "JumpStart");
                return controller;
            }

            private AnimationClip CreateClip(string name)
            {
                var clip = new AnimationClip { name = name, hideFlags = HideFlags.HideAndDontSave };
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    "m_LocalPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                _owned.Add(clip);
                if (name == "Move")
                {
                    ReferenceClip = clip;
                }

                return clip;
            }

            private static void AddTrigger(
                AnimatorController controller,
                AnimatorStateMachine machine,
                AnimatorState destination,
                string parameter)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
                var transition = machine.AddAnyStateTransition(destination);
                transition.hasExitTime = false;
                transition.duration = 0f;
                transition.AddCondition(AnimatorConditionMode.If, 0f, parameter);
            }
        }
    }
}
