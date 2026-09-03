using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyAnimationBindingEditorValidationTests
    {
        [Test]
        public void SerializedRowInitialization_UsesSparseNumericCueAndExplicitDefaults()
        {
            using var fixture = Fixture.Create();
            var serialized = new SerializedObject(fixture.Authoring);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = 1;
            var row = bindings.GetArrayElementAtIndex(0);

            EnemyAnimationBindingAuthoringEditor.InitializeNewBinding(row);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(row.FindPropertyRelative("cue").intValue, Is.EqualTo((int)EnemyAnimationCue.None));
            Assert.That(row.FindPropertyRelative("primaryDispatchMode").intValue,
                Is.EqualTo((int)EnemyAnimationDispatchMode.None));
            Assert.That(row.FindPropertyRelative("targetName").stringValue, Is.Empty);
            Assert.That(row.FindPropertyRelative("sustainedStateName").stringValue, Is.Empty);
            Assert.That(row.FindPropertyRelative("referenceClip").objectReferenceValue, Is.Null);
            Assert.That(row.FindPropertyRelative("animatorDurationSeconds").floatValue, Is.EqualTo(-1f));
        }

        [Test]
        public void InspectorPolicy_ShowsOnlySupportedConditionalFields()
        {
            Assert.That(
                EnemyAnimationBindingAuthoringEditor.ShouldShowSustainedState(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.Trigger),
                Is.True);
            Assert.That(
                EnemyAnimationBindingAuthoringEditor.ShouldShowSustainedState(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.State),
                Is.False);
            Assert.That(EnemyAnimationBindingAuthoringEditor.ShouldShowTiming(EnemyAnimationCue.ActionWindup),
                Is.True);
            Assert.That(EnemyAnimationBindingAuthoringEditor.ShouldShowTiming(EnemyAnimationCue.Hit),
                Is.False);
        }

        [Test]
        public void InspectorNormalization_ClearsHiddenValuesAndMaintainsCrossFadeSentinel()
        {
            using var fixture = Fixture.Create();
            var serialized = new SerializedObject(fixture.Authoring);
            var bindings = serialized.FindProperty("bindings");
            var crossFade = serialized.FindProperty("defaultStateCrossFadeDurationSeconds");
            bindings.arraySize = 1;
            var row = bindings.GetArrayElementAtIndex(0);
            row.FindPropertyRelative("cue").intValue = (int)EnemyAnimationCue.Hit;
            row.FindPropertyRelative("primaryDispatchMode").intValue =
                (int)EnemyAnimationDispatchMode.Trigger;
            row.FindPropertyRelative("sustainedStateName").stringValue = "StaleState";
            row.FindPropertyRelative("animatorDurationSeconds").floatValue = 2f;
            row.FindPropertyRelative("referenceClip").objectReferenceValue = fixture.CreateClip("StaleClip");
            crossFade.floatValue = 0.5f;

            EnemyAnimationBindingAuthoringEditor.NormalizeBindings(bindings, crossFade);

            Assert.That(row.FindPropertyRelative("sustainedStateName").stringValue, Is.Empty);
            Assert.That(row.FindPropertyRelative("animatorDurationSeconds").floatValue, Is.EqualTo(-1f));
            Assert.That(row.FindPropertyRelative("referenceClip").objectReferenceValue, Is.Null);
            Assert.That(crossFade.floatValue, Is.EqualTo(-1f));

            row.FindPropertyRelative("cue").intValue = (int)EnemyAnimationCue.ActionWindup;
            row.FindPropertyRelative("primaryDispatchMode").intValue =
                (int)EnemyAnimationDispatchMode.State;
            EnemyAnimationBindingAuthoringEditor.NormalizeBindings(bindings, crossFade);
            Assert.That(crossFade.floatValue, Is.Zero);
        }

        [Test]
        public void InspectorDiagnostics_UsesAnimatorResolvedByDriver()
        {
            using var fixture = Fixture.Create();
            var fallbackDriver = new SerializedObject(fixture.Driver);
            fallbackDriver.FindProperty("animator").objectReferenceValue = null;
            fallbackDriver.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(fallbackDriver.FindProperty("animator").objectReferenceValue, Is.Null);
            Assert.That(
                EnemyAnimationBindingAuthoringEditor.ResolveAnimatorForDiagnostics(fixture.Authoring),
                Is.SameAs(fixture.Animator));
            fallbackDriver.Update();
            Assert.That(
                fallbackDriver.FindProperty("animator").objectReferenceValue,
                Is.Null,
                "Read-only Inspector diagnostics must not serialize the fallback Animator.");

            var alternateObject = new GameObject("ExplicitAnimator");
            alternateObject.transform.SetParent(fixture.Root.transform, false);
            var alternateAnimator = alternateObject.AddComponent<Animator>();
            var serializedDriver = new SerializedObject(fixture.Driver);
            serializedDriver.FindProperty("animator").objectReferenceValue = alternateAnimator;
            serializedDriver.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(
                EnemyAnimationBindingAuthoringEditor.ResolveAnimatorForDiagnostics(fixture.Authoring),
                Is.SameAs(alternateAnimator));
        }

        [Test]
        public void StructuredDiagnostics_RejectChildAndDisabledAuthoring()
        {
            using var disabledFixture = Fixture.Create();
            disabledFixture.ConfigureState("Move");
            disabledFixture.Authoring.enabled = false;
            Assert.That(Validate(disabledFixture), Does.Contain("authoring.disabled"));

            using var childFixture = Fixture.Create();
            UnityEngine.Object.DestroyImmediate(childFixture.Authoring);
            var child = new GameObject("ChildBinding");
            child.transform.SetParent(childFixture.Root.transform, false);
            var childAuthoring = child.AddComponent<EnemyAnimationBindingAuthoring>();
            childAuthoring.ConfigureForTests(
                new[]
                {
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.ActionWindup,
                        EnemyAnimationDispatchMode.State,
                        "Move"),
                },
                0f);

            var diagnostics = EnemyAnimationControllerBindingValidator.Validate(
                    childAuthoring,
                    childFixture.Animator)
                .Select(diagnostic => diagnostic.Code)
                .ToArray();
            Assert.That(diagnostics, Does.Contain("authoring.not-root"));
        }

        [Test]
        public void TriggerParameter_MustBeTriggerAndConsumedByEffectiveTransition()
        {
            using var fixture = Fixture.Create();
            var trap = fixture.AddLayerZeroState("Trap");
            fixture.Controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            var transition = fixture.LayerZero.AddAnyStateTransition(trap);
            transition.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "Fire"));
            fixture.Rebind();

            Assert.That(Validate(fixture), Is.Empty);

            transition.mute = true;
            Assert.That(Validate(fixture), Does.Contain("trigger.unconsumed"));
        }

        [Test]
        public void SoloTransition_ShadowsOtherwiseValidTriggerTransition()
        {
            using var fixture = Fixture.Create();
            var fireTrap = fixture.AddLayerZeroState("FireTrap");
            var soloTrap = fixture.AddLayerZeroState("SoloTrap");
            fixture.Controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            fixture.Controller.AddParameter("Other", AnimatorControllerParameterType.Trigger);
            var fire = fixture.LayerZero.AddAnyStateTransition(fireTrap);
            fire.AddCondition(AnimatorConditionMode.If, 0f, "Fire");
            var solo = fixture.LayerZero.AddAnyStateTransition(soloTrap);
            solo.solo = true;
            solo.AddCondition(AnimatorConditionMode.If, 0f, "Other");
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "Fire"));
            fixture.Rebind();

            Assert.That(Validate(fixture), Does.Contain("trigger.unconsumed"));
        }

        [Test]
        public void NestedStateMachineOutgoingTransition_ConsumesTriggerWhenEffective()
        {
            using var fixture = Fixture.Create();
            var nested = fixture.LayerZero.AddStateMachine("Nested");
            var nestedState = nested.AddState("NestedState");
            nestedState.motion = fixture.CreateClip("NestedState");
            var destination = fixture.LayerZero.AddStateMachine("Destination");
            destination.AddState("DestinationState").motion = fixture.CreateClip("DestinationState");
            fixture.Controller.AddParameter("LeaveNested", AnimatorControllerParameterType.Trigger);
            var transition = fixture.LayerZero.AddStateMachineTransition(nested, destination);
            transition.AddCondition(AnimatorConditionMode.If, 0f, "LeaveNested");
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "LeaveNested"));
            fixture.Rebind();

            Assert.That(Validate(fixture), Is.Empty);

            transition.mute = true;
            Assert.That(Validate(fixture), Does.Contain("trigger.unconsumed"));
        }

        [Test]
        public void TriggerValidation_RejectsDuplicateAndWrongTypeParameters()
        {
            using var duplicateFixture = Fixture.Create();
            duplicateFixture.Controller.parameters = new[]
            {
                new AnimatorControllerParameter
                {
                    name = "Duplicate",
                    type = AnimatorControllerParameterType.Trigger,
                },
                new AnimatorControllerParameter
                {
                    name = "Duplicate",
                    type = AnimatorControllerParameterType.Trigger,
                },
            };
            duplicateFixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "Duplicate"));
            duplicateFixture.Rebind();
            Assert.That(Validate(duplicateFixture), Does.Contain("parameter.duplicate"));

            using var wrongTypeFixture = Fixture.Create();
            wrongTypeFixture.Controller.AddParameter("WrongType", AnimatorControllerParameterType.Bool);
            wrongTypeFixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.Trigger,
                    "WrongType"));
            wrongTypeFixture.Rebind();
            Assert.That(Validate(wrongTypeFixture), Does.Contain("trigger.wrong-type"));
        }

        [Test]
        public void StateValidation_RejectsLayerOneOnlyAndAmbiguousLayerZeroNames()
        {
            using var layerOneFixture = Fixture.Create();
            layerOneFixture.AddLayerOneState("OnlyHigherLayer");
            layerOneFixture.ConfigureState("OnlyHigherLayer");
            layerOneFixture.Rebind();
            Assert.That(Validate(layerOneFixture), Does.Contain("state.layer1-only"));

            using var ambiguousFixture = Fixture.Create();
            ambiguousFixture.AddLayerZeroState("Duplicate");
            var nested = ambiguousFixture.LayerZero.AddStateMachine("Nested");
            nested.AddState("Duplicate").motion = ambiguousFixture.CreateClip("NestedDuplicate");
            ambiguousFixture.ConfigureState("Duplicate");
            ambiguousFixture.Rebind();
            Assert.That(Validate(ambiguousFixture), Does.Contain("state.ambiguous"));
        }

        [Test]
        public void OverrideController_UsesEffectiveClipAndNestedConstructionFlattensToBaseController()
        {
            using var fixture = Fixture.Create();
            var baseClip = fixture.LayerZero.defaultState.motion as AnimationClip;
            var firstOverrideClip = fixture.CreateClip("FirstOverride");
            var secondOverrideClip = fixture.CreateClip("SecondOverride");
            var first = new AnimatorOverrideController(fixture.Controller);
            first[baseClip] = firstOverrideClip;
            fixture.Own(first);
            fixture.Animator.runtimeAnimatorController = first;
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "Move",
                    animatorDurationSeconds: -1f,
                    referenceClip: firstOverrideClip),
                crossFade: 0f);
            fixture.Rebind();

            Assert.That(first.animationClips, Does.Contain(firstOverrideClip));
            Assert.That(Validate(fixture), Is.Empty);

            var second = new AnimatorOverrideController(first);
            fixture.Own(second);
            Assert.That(second.runtimeAnimatorController, Is.SameAs(fixture.Controller));
            second[baseClip] = secondOverrideClip;
            fixture.Animator.runtimeAnimatorController = second;
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "Move",
                    animatorDurationSeconds: -1f,
                    referenceClip: secondOverrideClip),
                crossFade: 0f);
            fixture.Rebind();

            Assert.That(second.animationClips, Does.Contain(secondOverrideClip));
            Assert.That(Validate(fixture), Is.Empty);

            var invalidReference = fixture.CreateClip("NotEffective");
            fixture.Configure(
                EnemyAnimationCueBinding.CreateForTests(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.State,
                    "Move",
                    animatorDurationSeconds: -1f,
                    referenceClip: invalidReference),
                crossFade: 0f);
            Assert.That(Validate(fixture), Does.Contain("clip.not-effective"));
        }

        [Test]
        public void OverrideControllerWithoutBase_IsRejected()
        {
            var controller = new AnimatorOverrideController();
            try
            {
                Assert.That(
                    EnemyAnimationControllerBindingValidator.TryUnwrapController(
                        controller,
                        out _,
                        out var error),
                    Is.False);
                Assert.That(error, Does.Contain("no base"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller);
            }
        }

        private static string[] Validate(Fixture fixture)
        {
            return EnemyAnimationControllerBindingValidator.Validate(fixture.Authoring, fixture.Animator)
                .Select(diagnostic => diagnostic.Code)
                .ToArray();
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<UnityEngine.Object> _owned = new();

            private Fixture(GameObject root)
            {
                Root = root;
            }

            public GameObject Root { get; }
            public Animator Animator { get; private set; }
            public EnemyAnimatorDriver Driver { get; private set; }
            public EnemyAnimationBindingAuthoring Authoring { get; private set; }
            public AnimatorController Controller { get; private set; }
            public AnimatorStateMachine LayerZero => Controller.layers[0].stateMachine;

            public static Fixture Create()
            {
                var fixture = new Fixture(new GameObject(nameof(EnemyAnimationBindingEditorValidationTests)));
                fixture.Build();
                return fixture;
            }

            public AnimatorState AddLayerZeroState(string name)
            {
                var state = LayerZero.AddState(name);
                state.motion = CreateClip(name);
                return state;
            }

            public void AddLayerOneState(string name)
            {
                var stateMachine = new AnimatorStateMachine { hideFlags = HideFlags.HideAndDontSave };
                Own(stateMachine);
                var state = stateMachine.AddState(name);
                state.motion = CreateClip(name);
                var layers = Controller.layers.ToList();
                layers.Add(new AnimatorControllerLayer
                {
                    name = "Layer 1",
                    defaultWeight = 1f,
                    stateMachine = stateMachine,
                });
                Controller.layers = layers.ToArray();
            }

            public AnimationClip CreateClip(string name)
            {
                var clip = new AnimationClip { name = name, hideFlags = HideFlags.HideAndDontSave };
                clip.SetCurve(
                    string.Empty,
                    typeof(Transform),
                    "m_LocalPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));
                Own(clip);
                return clip;
            }

            public void ConfigureState(string stateName)
            {
                Configure(
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.ActionWindup,
                        EnemyAnimationDispatchMode.State,
                        stateName),
                    0f);
            }

            public void Configure(EnemyAnimationCueBinding binding, float crossFade = -1f)
            {
                Authoring.ConfigureForTests(new[] { binding }, crossFade);
            }

            public void Rebind()
            {
                Animator.Rebind();
                Animator.Update(0f);
            }

            public void Own(UnityEngine.Object value)
            {
                _owned.Add(value);
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

            private void Build()
            {
                Animator = Root.AddComponent<Animator>();
                Driver = Root.AddComponent<EnemyAnimatorDriver>();
                Authoring = Root.AddComponent<EnemyAnimationBindingAuthoring>();
                var stateMachine = new AnimatorStateMachine { hideFlags = HideFlags.HideAndDontSave };
                Own(stateMachine);
                var move = stateMachine.AddState("Move");
                move.motion = CreateClip("Move");
                stateMachine.defaultState = move;
                Controller = new AnimatorController
                {
                    hideFlags = HideFlags.HideAndDontSave,
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
                Own(Controller);
                Animator.runtimeAnimatorController = Controller;
            }
        }
    }
}
