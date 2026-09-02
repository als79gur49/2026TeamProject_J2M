using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    [Category("Full")]
    public sealed class EnemyViewAnimatorControllerContractTests
    {
        private const int ExpectedProductionViewCount = 10;
        private const string CampaignMainCatalogPath =
            StageContentPaths.SharedEnemyPresentationRoot + "/Catalogs/EnemyPresentationCatalog_CampaignMain.asset";
        private const string ProductionPrefabRoot =
            StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs";
        private static readonly ParameterBinding[] DriverParameters =
        {
            new ParameterBinding("EnemyAiMode", AnimatorControllerParameterType.Int),
            new ParameterBinding("EnemyActionKind", AnimatorControllerParameterType.Int),
            new ParameterBinding("EnemyJumpPhase", AnimatorControllerParameterType.Int),
            new ParameterBinding("EnemyChargePhase", AnimatorControllerParameterType.Int),
            new ParameterBinding("IsMoving", AnimatorControllerParameterType.Bool),
        };

        // This is a View presentation baseline. Do not infer it from EnemyAiProfile or gameplay capabilities.
        private static readonly ViewContract[] ProductionContracts =
        {
            new ViewContract(
                "black_eye", "EnemyView_BlackEye.prefab",
                new[]
                {
                    new StateBinding("windupStateName", "Windup"),
                    new StateBinding("recoveryStateName", "Recover"),
                },
                HitAndDeathTriggers(),
                Parameters("EnemyAiMode", "IsMoving"),
                requiresNamedStateCrossFade: true),
            new ViewContract(
                "startis", "EnemyView_Startis.prefab",
                Array.Empty<StateBinding>(), HitAndDeathTriggers(), Parameters("IsMoving")),
            new ViewContract(
                "rocket_face", "EnemyView_RocketFace.prefab",
                new[]
                {
                    new StateBinding("windupStateName", "Windup"),
                    new StateBinding("chargeActiveStateName", "Charge"),
                    new StateBinding("recoveryStateName", "Recover"),
                },
                HitAndDeathTriggers(),
                Parameters("EnemyAiMode", "EnemyChargePhase", "IsMoving"),
                requiresNamedStateCrossFade: true),
            new ViewContract(
                "astreton", "EnemyView_Astreton.prefab",
                new[]
                {
                    new StateBinding("jumpWindupStateName", "JumpWindup"),
                    new StateBinding("jumpAirborneStateName", "JumpAirborne"),
                },
                new[]
                {
                    new TriggerBinding("jumpWindupTriggerName", "JumpWindup"),
                    new TriggerBinding("jumpAirborneTriggerName", "JumpAirborne"),
                    new TriggerBinding("attackTriggerName", "Attack"),
                    new TriggerBinding("hitTriggerName", "Hit"),
                    new TriggerBinding("deathTriggerName", "Death"),
                },
                Parameters("EnemyAiMode", "IsMoving")),
            new ViewContract(
                "dr_saturn", "EnemyView_DrSaturn.prefab",
                Array.Empty<StateBinding>(),
                new[]
                {
                    new TriggerBinding("windupTriggerName", "Windup"),
                    new TriggerBinding("recoveryTriggerName", "Recover"),
                    new TriggerBinding("hitTriggerName", "Hit"),
                    new TriggerBinding("deathTriggerName", "Death"),
                },
                Parameters("IsMoving")),
            new ViewContract(
                "j_peter", "EnemyView_JPeter.prefab",
                Array.Empty<StateBinding>(), HitAndDeathTriggers(), Parameters("IsMoving")),
            new ViewContract(
                "sunwheel", "EnemyView_Sunwheel.prefab",
                Array.Empty<StateBinding>(), HitAndDeathTriggers(), Array.Empty<ParameterBinding>()),
            new ViewContract(
                "kali", "EnemyView_Kali.prefab",
                Array.Empty<StateBinding>(), Array.Empty<TriggerBinding>(), Parameters("IsMoving")),
            new ViewContract(
                "secbot", "EnemyView_SecBot.prefab",
                Array.Empty<StateBinding>(), Array.Empty<TriggerBinding>(), Parameters("IsMoving")),
            new ViewContract(
                "nebulous", "EnemyView_Nebulous.prefab",
                new[]
                {
                    new StateBinding("windupStateName", "Fly_Start"),
                    new StateBinding("recoveryStateName", "Fly_Done"),
                    new StateBinding("glideWindupStateName", "Fly_Start"),
                    new StateBinding("glideActiveStateName", "Fly_Loop"),
                    new StateBinding("glideRecoveryStateName", "Fly_Done"),
                },
                Array.Empty<TriggerBinding>(),
                Array.Empty<ParameterBinding>(),
                requiresNamedStateCrossFade: true,
                disabledTriggerFields: new[] { "hitTriggerName", "deathTriggerName" }),
        };

        [Test]
        public void CampaignMainCatalog_ContainsExactProductionIdToPrefabMappingWithoutDuplicates()
        {
            var catalog = LoadCatalog();
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(catalog, diagnostics);

            Assert.That(
                catalog.Entries,
                Has.Length.EqualTo(ExpectedProductionViewCount),
                $"'{CampaignMainCatalogPath}' must contain exactly {ExpectedProductionViewCount} production views.");

            foreach (var contract in ProductionContracts)
            {
                if (!entriesById.TryGetValue(contract.PresentationId, out var entry))
                {
                    diagnostics.Add($"presentationId='{contract.PresentationId}': expected entry is missing.");
                    continue;
                }

                var actualPath = AssetDatabase.GetAssetPath(entry.ViewPrefab);
                if (!string.Equals(actualPath, contract.PrefabPath, StringComparison.Ordinal))
                {
                    diagnostics.Add(Format(contract, actualPath, "<unresolved>", nameof(entry.ViewPrefab),
                        $"expected exact production prefab '{contract.PrefabPath}'."));
                }
            }

            var expectedIds = new HashSet<string>(
                ProductionContracts.Select(contract => contract.PresentationId), StringComparer.Ordinal);
            foreach (var actualId in entriesById.Keys.Where(actualId => !expectedIds.Contains(actualId)))
            {
                diagnostics.Add($"presentationId='{actualId}': unexpected production entry.");
            }

            AssertEmpty(diagnostics, "Campaign production catalog identity violations");
        }

        [Test]
        public void CampaignMainEnemyViews_HaveExpectedRootDriverAnimatorAndControllerWiring()
        {
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(LoadCatalog(), diagnostics);
            foreach (var contract in ProductionContracts)
            {
                if (!TryGetContext(contract, entriesById, diagnostics, out var context))
                {
                    continue;
                }

                TryUnwrapController(contract, context.AssignedController, diagnostics, out _);
            }

            AssertEmpty(diagnostics, "Campaign production Animator wiring violations");
        }

        [Test]
        public void CampaignMainEnemyViews_ModelRootsMeetDeathMotionPoseFreezeSafetyContract()
        {
            var diagnostics = new List<string>();
            foreach (var contract in ProductionContracts)
            {
                var view = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(contract.PrefabPath);
                if (view == null)
                {
                    diagnostics.Add($"presentationId='{contract.PresentationId}': missing prefab '{contract.PrefabPath}'.");
                    continue;
                }

                if (view.ModelRoot == null)
                {
                    diagnostics.Add($"presentationId='{contract.PresentationId}': prefab ModelRoot is missing.");
                    continue;
                }

                if (GameplayVfxPooledInstance.TryFindUnsupportedPoseWriter(
                        view.ModelRoot,
                        out var unsupportedPoseWriter))
                {
                    diagnostics.Add(
                        $"presentationId='{contract.PresentationId}', prefab='{contract.PrefabPath}': " +
                        $"ModelRoot component '{unsupportedPoseWriter.GetType().Name}' on " +
                        $"'{unsupportedPoseWriter.gameObject.name}' would force every DeathMotion to the authored fallback.");
                }
            }

            AssertEmpty(diagnostics, "Campaign production DeathMotion pose-freeze safety violations");
        }

        [Test]
        public void CampaignMainEnemyViews_ActiveStateAndParameterBindingsAreDispatchReachable()
        {
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(LoadCatalog(), diagnostics);
            foreach (var contract in ProductionContracts)
            {
                if (!TryGetControllerContext(contract, entriesById, diagnostics, out var context, out var controller))
                {
                    continue;
                }

                var graph = BuildGraph(controller);
                ValidateActiveStates(contract, context, graph, diagnostics);
                ValidateNamedStateDispatchMode(contract, context, diagnostics);
                ValidateParameters(contract, controller, graph, diagnostics);
            }

            AssertEmpty(diagnostics, "Campaign production active Animator dispatch violations");
        }

        [Test]
        public void CampaignMainEnemyViews_ActiveTriggerBindingsAreTypedAndConsumedByTransitions()
        {
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(LoadCatalog(), diagnostics);
            foreach (var contract in ProductionContracts)
            {
                if (!TryGetControllerContext(contract, entriesById, diagnostics, out var context, out var controller))
                {
                    continue;
                }

                var graph = BuildGraph(controller);
                foreach (var binding in contract.ActiveTriggers)
                {
                    ValidateActiveTrigger(contract, context, controller, graph, binding, diagnostics);
                }

                var activeFields = new HashSet<string>(
                    contract.ActiveTriggers.Select(binding => binding.FieldName), StringComparer.Ordinal);
                foreach (var fieldName in TriggerBinding.AllFieldNames.Where(field => !activeFields.Contains(field)))
                {
                    var triggerName = ReadDriverString(contract, context, fieldName, diagnostics);
                    if (!string.IsNullOrWhiteSpace(triggerName))
                    {
                        ValidateAuthoredTrigger(
                            contract, context, controller, graph, fieldName, triggerName, diagnostics);
                    }
                }
            }

            AssertEmpty(diagnostics, "Campaign production active Animator trigger violations");
        }

        [Test]
        public void CampaignMainEnemyViews_ExplicitlyDisabledTriggerBindingsRemainBlank()
        {
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(LoadCatalog(), diagnostics);
            foreach (var contract in ProductionContracts)
            {
                if (!TryGetContext(contract, entriesById, diagnostics, out var context))
                {
                    continue;
                }

                foreach (var fieldName in contract.DisabledTriggerFields)
                {
                    var value = ReadDriverString(contract, context, fieldName, diagnostics);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        diagnostics.Add(Format(contract, contract.PrefabPath,
                            DescribeController(context.AssignedController), fieldName,
                            $"binding is disabled by the View matrix and must be blank, but is '{value}'."));
                    }
                }
            }

            AssertEmpty(diagnostics, "Campaign production explicitly disabled Animator binding violations");
        }

        [Test]
        public void ControllerGraph_ConsumesConditionsOnlyFromExecutableTransitionSet()
        {
            var stateMachine = new AnimatorStateMachine
            {
                name = "TransitionExecutionContract",
                hideFlags = HideFlags.HideAndDontSave,
            };
            var controller = new AnimatorController
            {
                name = "TransitionExecutionContractController",
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

            try
            {
                var source = stateMachine.AddState("Source");
                var activeSource = stateMachine.AddState("ActiveSource");
                var destination = stateMachine.AddState("Destination");
                stateMachine.defaultState = source;

                var mutedTransition = source.AddTransition(destination);
                mutedTransition.mute = true;
                mutedTransition.AddCondition(AnimatorConditionMode.If, 0f, "MutedCondition");

                var shadowedTransition = source.AddTransition(destination);
                shadowedTransition.AddCondition(AnimatorConditionMode.If, 0f, "ShadowedCondition");

                var soloTransition = source.AddTransition(destination);
                soloTransition.solo = true;
                soloTransition.AddCondition(AnimatorConditionMode.If, 0f, "SoloCondition");

                var activeTransition = activeSource.AddTransition(destination);
                activeTransition.AddCondition(AnimatorConditionMode.If, 0f, "ActiveCondition");

                var graph = BuildGraph(controller);

                Assert.That(graph.ConsumedConditions, Does.Contain("SoloCondition"));
                Assert.That(graph.ConsumedConditions, Does.Contain("ActiveCondition"));
                Assert.That(graph.ConsumedConditions, Does.Not.Contain("MutedCondition"));
                Assert.That(graph.ConsumedConditions, Does.Not.Contain("ShadowedCondition"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controller);
                UnityEngine.Object.DestroyImmediate(stateMachine);
            }
        }

        private static EnemyPresentationCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(CampaignMainCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing catalog at '{CampaignMainCatalogPath}'.");
            return catalog;
        }

        private static Dictionary<string, EnemyPresentationCatalogEntry> IndexEntries(
            EnemyPresentationCatalog catalog,
            ICollection<string> diagnostics)
        {
            var byId = new Dictionary<string, EnemyPresentationCatalogEntry>(StringComparer.Ordinal);
            var idByPrefabPath = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in catalog.Entries)
            {
                var id = EnemyPresentationCatalogResolver.NormalizePresentationId(entry.PresentationId);
                if (string.IsNullOrEmpty(id))
                {
                    diagnostics.Add("presentationId='<blank>': production IDs must be non-empty.");
                    continue;
                }

                if (!string.Equals(entry.PresentationId, id, StringComparison.Ordinal))
                {
                    diagnostics.Add(
                        $"presentationId='{entry.PresentationId}': production IDs must already be normalized as '{id}'.");
                }

                if (!byId.TryAdd(id, entry))
                {
                    diagnostics.Add($"presentationId='{id}': production IDs must be unique.");
                }

                if (entry.ViewPrefab == null)
                {
                    diagnostics.Add($"presentationId='{id}': production prefab must be non-null.");
                    continue;
                }

                var prefabPath = AssetDatabase.GetAssetPath(entry.ViewPrefab);
                if (!idByPrefabPath.TryAdd(prefabPath, id))
                {
                    diagnostics.Add($"presentationId='{id}', prefab='{prefabPath}': prefab is reused by " +
                                    $"presentationId='{idByPrefabPath[prefabPath]}'.");
                }
            }

            return byId;
        }

        private static bool TryGetControllerContext(
            ViewContract contract,
            IReadOnlyDictionary<string, EnemyPresentationCatalogEntry> entriesById,
            ICollection<string> diagnostics,
            out EntryContext context,
            out AnimatorController controller)
        {
            controller = null;
            return TryGetContext(contract, entriesById, diagnostics, out context) &&
                   TryUnwrapController(contract, context.AssignedController, diagnostics, out controller);
        }

        private static bool TryGetContext(
            ViewContract contract,
            IReadOnlyDictionary<string, EnemyPresentationCatalogEntry> entriesById,
            ICollection<string> diagnostics,
            out EntryContext context)
        {
            context = default;
            if (!entriesById.TryGetValue(contract.PresentationId, out var entry) || entry.ViewPrefab == null)
            {
                return false;
            }

            var prefab = entry.ViewPrefab;
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            var drivers = prefab.GetComponents<EnemyAnimatorDriver>();
            if (drivers.Length != 1)
            {
                diagnostics.Add(Format(contract, prefabPath, "<unresolved>", nameof(EnemyAnimatorDriver),
                    $"prefab root requires exactly one driver, but found {drivers.Length}."));
                return false;
            }

            var driver = drivers[0];
            var serializedDriver = new SerializedObject(driver);
            var animatorProperty = serializedDriver.FindProperty("animator");
            if (animatorProperty == null)
            {
                diagnostics.Add(Format(contract, prefabPath, "<unresolved>", "animator",
                    "driver no longer exposes its serialized Animator reference."));
                return false;
            }

            var animator = animatorProperty.objectReferenceValue as Animator;
            if (animator != null && !IsInHierarchy(prefab.transform, animator.transform))
            {
                diagnostics.Add(Format(contract, prefabPath, DescribeController(animator.runtimeAnimatorController),
                    animatorProperty.name, "explicit Animator must belong to the prefab hierarchy."));
                return false;
            }

            if (animator == null)
            {
                var candidates = prefab.GetComponentsInChildren<Animator>();
                if (candidates.Length != 1)
                {
                    diagnostics.Add(Format(contract, prefabPath, "<unresolved>", animatorProperty.name,
                        $"null Animator reference requires exactly one hierarchy candidate, but found {candidates.Length}."));
                    return false;
                }

                animator = candidates[0];
            }

            if (animator.runtimeAnimatorController == null)
            {
                diagnostics.Add(Format(contract, prefabPath, "<null>",
                    nameof(Animator.runtimeAnimatorController), "resolved Animator requires a controller."));
                return false;
            }

            context = new EntryContext(driver, animator, animator.runtimeAnimatorController);
            return true;
        }

        private static bool TryUnwrapController(
            ViewContract contract,
            RuntimeAnimatorController assigned,
            ICollection<string> diagnostics,
            out AnimatorController controller)
        {
            var visited = new HashSet<RuntimeAnimatorController>();
            var current = assigned;
            while (current is AnimatorOverrideController overrideController)
            {
                if (!visited.Add(current))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(assigned),
                        nameof(Animator.runtimeAnimatorController), "AnimatorOverrideController chain contains a cycle."));
                    controller = null;
                    return false;
                }

                current = overrideController.runtimeAnimatorController;
            }

            controller = current as AnimatorController;
            if (controller != null)
            {
                return true;
            }

            diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(assigned),
                nameof(Animator.runtimeAnimatorController),
                "topology inspection requires AnimatorController or safely unwrapped AnimatorOverrideController."));
            return false;
        }

        private static ControllerGraph BuildGraph(AnimatorController controller)
        {
            var states = new List<StateDescriptor>();
            var conditions = new HashSet<string>(StringComparer.Ordinal);
            for (var layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
            {
                var layer = controller.layers[layerIndex];
                CollectGraph(layer.stateMachine, layer.name, layerIndex, states, conditions);
            }

            return new ControllerGraph(states, conditions);
        }

        private static void CollectGraph(
            AnimatorStateMachine stateMachine,
            string stateMachinePath,
            int layerIndex,
            ICollection<StateDescriptor> states,
            ISet<string> conditions)
        {
            foreach (var childState in stateMachine.states)
            {
                states.Add(new StateDescriptor(childState.state,
                    stateMachinePath + "." + childState.state.name, layerIndex));
                CollectConditions(childState.state.transitions, conditions);
            }

            CollectConditions(stateMachine.anyStateTransitions, conditions);
            CollectConditions(stateMachine.entryTransitions, conditions);
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                CollectConditions(stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine), conditions);
                CollectGraph(childStateMachine.stateMachine,
                    stateMachinePath + "." + childStateMachine.stateMachine.name,
                    layerIndex, states, conditions);
            }
        }

        private static void CollectConditions<T>(IEnumerable<T> transitions, ISet<string> conditions)
            where T : AnimatorTransitionBase
        {
            var transitionSet = transitions.Where(transition => transition != null).ToArray();
            var hasEnabledSoloTransition = transitionSet.Any(transition =>
                transition.solo && !transition.mute);
            foreach (var transition in transitionSet)
            {
                if (transition.mute ||
                    (hasEnabledSoloTransition && !transition.solo))
                {
                    continue;
                }

                foreach (var condition in transition.conditions)
                {
                    if (!string.IsNullOrWhiteSpace(condition.parameter))
                    {
                        conditions.Add(condition.parameter);
                    }
                }
            }
        }

        private static void ValidateActiveStates(
            ViewContract contract,
            EntryContext context,
            ControllerGraph graph,
            ICollection<string> diagnostics)
        {
            foreach (var binding in contract.ActiveStates)
            {
                var stateName = ReadDriverString(contract, context, binding.FieldName, diagnostics);
                if (!string.Equals(stateName, binding.ExpectedName, StringComparison.Ordinal))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.FieldName,
                        $"expected active state '{binding.ExpectedName}', but is '{stateName}'."));
                    continue;
                }

                var matches = graph.States.Where(state => state.LayerIndex == 0 &&
                    string.Equals(state.State.name, stateName, StringComparison.Ordinal)).ToArray();
                if (matches.Length != 1)
                {
                    var detail = matches.Length == 0
                        ? "is missing from controller layer 0"
                        : "is ambiguous: " + string.Join(", ", matches.Select(match => match.FullPath));
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.FieldName,
                        $"active state '{stateName}' {detail}."));
                    continue;
                }

                if (!IsDriverReachable(context.AssignedController, stateName))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.FieldName,
                        $"state '{matches[0].FullPath}' is not reachable by the driver's supported layer-0 hashes."));
                }

                if (!HasEffectiveMotion(context.AssignedController, matches[0].State.motion))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.FieldName,
                        $"active direct state '{matches[0].FullPath}' requires an effective Motion."));
                }
            }
        }

        private static bool IsDriverReachable(RuntimeAnimatorController controller, string stateName)
        {
            var probeObject = new GameObject("EnemyAnimatorContractStateProbe")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                var animator = probeObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0f);
                return animator.HasState(0, Animator.StringToHash(stateName)) ||
                       animator.HasState(0, Animator.StringToHash("Base Layer." + stateName)) ||
                       animator.HasState(0, Animator.StringToHash("Base Layer.Locomotion." + stateName));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probeObject);
            }
        }

        private static bool HasEffectiveMotion(RuntimeAnimatorController assigned, Motion baseMotion)
        {
            if (baseMotion == null)
            {
                return false;
            }

            if (!(assigned is AnimatorOverrideController overrideController) ||
                !(baseMotion is AnimationClip baseClip))
            {
                return true;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            var overridePair = overrides.FirstOrDefault(pair => pair.Key == baseClip);
            return overridePair.Key == null || overridePair.Value != null || baseClip != null;
        }

        private static void ValidateParameters(
            ViewContract contract,
            AnimatorController controller,
            ControllerGraph graph,
            ICollection<string> diagnostics)
        {
            var byName = new Dictionary<string, AnimatorControllerParameter>(StringComparer.Ordinal);
            foreach (var parameter in controller.parameters)
            {
                if (!byName.TryAdd(parameter.name, parameter))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(controller),
                        parameter.name, "controller parameters must not contain duplicate names."));
                }
            }

            var requiredNames = new HashSet<string>(
                contract.RequiredParameters.Select(binding => binding.Name), StringComparer.Ordinal);
            foreach (var binding in contract.RequiredParameters)
            {
                ValidateParameter(contract, controller, byName, binding, true, diagnostics);
                if (!graph.ConsumedConditions.Contains(binding.Name))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(controller), binding.Name,
                        "required runtime parameter is not consumed by any transition condition."));
                }
            }

            foreach (var binding in DriverParameters.Where(binding =>
                         !requiredNames.Contains(binding.Name) && byName.ContainsKey(binding.Name)))
            {
                ValidateParameter(contract, controller, byName, binding, false, diagnostics);
            }
        }

        private static void ValidateNamedStateDispatchMode(
            ViewContract contract,
            EntryContext context,
            ICollection<string> diagnostics)
        {
            if (!contract.RequiresNamedStateCrossFade)
            {
                return;
            }

            if (!context.Driver.TryGetComponent<EnemyAnimationTimingAuthoring>(out var authoring) ||
                authoring == null)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), nameof(EnemyAnimationTimingAuthoring),
                    "direct-state dispatch requires timing authoring with an enabled cross-fade override."));
                return;
            }

            if (!EnemyAnimationTimingAuthoring.IsStateTransitionCrossFadeOverride(
                    authoring.StateTransitionCrossFadeDurationSeconds))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController),
                    nameof(EnemyAnimationTimingAuthoring.StateTransitionCrossFadeDurationSeconds),
                    "direct-state dispatch requires a nonnegative cross-fade override; exact tuning remains editable."));
            }
        }

        private static void ValidateParameter(
            ViewContract contract,
            AnimatorController controller,
            IReadOnlyDictionary<string, AnimatorControllerParameter> byName,
            ParameterBinding binding,
            bool required,
            ICollection<string> diagnostics)
        {
            if (!byName.TryGetValue(binding.Name, out var parameter))
            {
                if (required)
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(controller), binding.Name,
                        $"required {binding.ExpectedType} parameter is missing."));
                }
                return;
            }

            if (parameter.type != binding.ExpectedType)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath, DescribeController(controller), binding.Name,
                    $"{(required ? "required" : "optional")} parameter must be {binding.ExpectedType}, " +
                    $"but is {parameter.type}."));
            }
        }

        private static void ValidateActiveTrigger(
            ViewContract contract,
            EntryContext context,
            AnimatorController controller,
            ControllerGraph graph,
            TriggerBinding binding,
            ICollection<string> diagnostics)
        {
            var triggerName = ReadDriverString(contract, context, binding.FieldName, diagnostics);
            if (!string.Equals(triggerName, binding.ExpectedName, StringComparison.Ordinal))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.FieldName,
                    $"expected active trigger '{binding.ExpectedName}', but is '{triggerName}'."));
                return;
            }

            ValidateAuthoredTrigger(
                contract, context, controller, graph, binding.FieldName, triggerName, diagnostics);
        }

        private static void ValidateAuthoredTrigger(
            ViewContract contract,
            EntryContext context,
            AnimatorController controller,
            ControllerGraph graph,
            string fieldName,
            string triggerName,
            ICollection<string> diagnostics)
        {
            var parameters = controller.parameters.Where(parameter =>
                string.Equals(parameter.name, triggerName, StringComparison.Ordinal)).ToArray();
            if (parameters.Length != 1)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), fieldName,
                    $"trigger '{triggerName}' requires exactly one parameter, but found {parameters.Length}."));
                return;
            }

            if (parameters[0].type != AnimatorControllerParameterType.Trigger)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), fieldName,
                    $"trigger '{triggerName}' must be Trigger, but is {parameters[0].type}."));
            }

            if (!graph.ConsumedConditions.Contains(triggerName))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), fieldName,
                    $"trigger '{triggerName}' is not consumed by any transition condition."));
            }
        }

        private static string ReadDriverString(
            ViewContract contract,
            EntryContext context,
            string fieldName,
            ICollection<string> diagnostics)
        {
            var property = new SerializedObject(context.Driver).FindProperty(fieldName);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), fieldName,
                    "driver no longer exposes the expected serialized string binding."));
                return null;
            }

            return property.stringValue;
        }

        private static bool IsInHierarchy(Transform root, Transform candidate)
        {
            for (var current = candidate; current != null; current = current.parent)
            {
                if (current == root)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AssertEmpty(ICollection<string> diagnostics, string message)
        {
            Assert.That(diagnostics, Is.Empty, message + ":\n" + string.Join("\n", diagnostics));
        }

        private static string DescribeController(RuntimeAnimatorController controller)
        {
            return controller == null ? "<null>" : AssetDatabase.GetAssetPath(controller);
        }

        private static string Format(
            ViewContract contract,
            string prefabPath,
            string controllerPath,
            string fieldName,
            string message)
        {
            return $"presentationId='{contract.PresentationId}', prefab='{prefabPath}', " +
                   $"controller='{controllerPath}', field='{fieldName}': {message}";
        }

        private static TriggerBinding[] HitAndDeathTriggers()
        {
            return new[]
            {
                new TriggerBinding("hitTriggerName", "Hit"),
                new TriggerBinding("deathTriggerName", "Death"),
            };
        }

        private static ParameterBinding[] Parameters(params string[] names)
        {
            return names.Select(name => DriverParameters.Single(binding =>
                string.Equals(binding.Name, name, StringComparison.Ordinal))).ToArray();
        }

        private readonly struct EntryContext
        {
            public EntryContext(EnemyAnimatorDriver driver, Animator animator, RuntimeAnimatorController assignedController)
            {
                Driver = driver;
                Animator = animator;
                AssignedController = assignedController;
            }
            public EnemyAnimatorDriver Driver { get; }
            public Animator Animator { get; }
            public RuntimeAnimatorController AssignedController { get; }
        }

        private sealed class ViewContract
        {
            public ViewContract(
                string presentationId,
                string prefabName,
                StateBinding[] activeStates,
                TriggerBinding[] activeTriggers,
                ParameterBinding[] requiredParameters,
                bool requiresNamedStateCrossFade = false,
                string[] disabledTriggerFields = null)
            {
                PresentationId = presentationId;
                PrefabPath = ProductionPrefabRoot + "/" + prefabName;
                ActiveStates = activeStates;
                ActiveTriggers = activeTriggers;
                RequiredParameters = requiredParameters;
                RequiresNamedStateCrossFade = requiresNamedStateCrossFade;
                DisabledTriggerFields = disabledTriggerFields ?? Array.Empty<string>();
            }
            public string PresentationId { get; }
            public string PrefabPath { get; }
            public StateBinding[] ActiveStates { get; }
            public TriggerBinding[] ActiveTriggers { get; }
            public ParameterBinding[] RequiredParameters { get; }
            public bool RequiresNamedStateCrossFade { get; }
            public string[] DisabledTriggerFields { get; }
        }

        private readonly struct StateBinding
        {
            public StateBinding(string fieldName, string expectedName)
            {
                FieldName = fieldName;
                ExpectedName = expectedName;
            }
            public string FieldName { get; }
            public string ExpectedName { get; }
        }

        private readonly struct TriggerBinding
        {
            public static readonly string[] AllFieldNames =
            {
                "windupTriggerName", "jumpWindupTriggerName", "jumpAirborneTriggerName",
                "attackTriggerName", "recoveryTriggerName", "hitTriggerName", "deathTriggerName",
            };
            public TriggerBinding(string fieldName, string expectedName)
            {
                FieldName = fieldName;
                ExpectedName = expectedName;
            }
            public string FieldName { get; }
            public string ExpectedName { get; }
        }

        private readonly struct ParameterBinding
        {
            public ParameterBinding(string name, AnimatorControllerParameterType expectedType)
            {
                Name = name;
                ExpectedType = expectedType;
            }
            public string Name { get; }
            public AnimatorControllerParameterType ExpectedType { get; }
        }

        private readonly struct StateDescriptor
        {
            public StateDescriptor(AnimatorState state, string fullPath, int layerIndex)
            {
                State = state;
                FullPath = fullPath;
                LayerIndex = layerIndex;
            }
            public AnimatorState State { get; }
            public string FullPath { get; }
            public int LayerIndex { get; }
        }

        private sealed class ControllerGraph
        {
            public ControllerGraph(
                IReadOnlyList<StateDescriptor> states,
                IReadOnlyCollection<string> consumedConditions)
            {
                States = states;
                ConsumedConditions = consumedConditions;
            }
            public IReadOnlyList<StateDescriptor> States { get; }
            public IReadOnlyCollection<string> ConsumedConditions { get; }
        }
    }
}
