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
                    State(EnemyAnimationCue.ActionWindup, "Windup"),
                    State(EnemyAnimationCue.ActionRecovery, "Recover"),
                },
                HitAndDeathTriggers(),
                Parameters("EnemyAiMode", "IsMoving")),
            new ViewContract(
                "startis", "EnemyView_Startis.prefab",
                Array.Empty<StateBinding>(), HitAndDeathTriggers(), Parameters("IsMoving")),
            new ViewContract(
                "rocket_face", "EnemyView_RocketFace.prefab",
                new[]
                {
                    State(EnemyAnimationCue.ChargeWindup, "Windup"),
                    State(EnemyAnimationCue.ChargeActive, "Charge"),
                    State(EnemyAnimationCue.ChargeRecovery, "Recover"),
                },
                HitAndDeathTriggers(),
                Parameters("EnemyAiMode", "EnemyChargePhase", "IsMoving")),
            new ViewContract(
                "astreton", "EnemyView_Astreton.prefab",
                new[]
                {
                    State(EnemyAnimationCue.JumpWindup, "JumpWindup"),
                    State(EnemyAnimationCue.JumpAirborne, "JumpAirborne"),
                    State(EnemyAnimationCue.JumpLanding, "Move"),
                },
                new[]
                {
                    Trigger(EnemyAnimationCue.ActionExecute, "Attack"),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death"),
                },
                Parameters("EnemyAiMode", "IsMoving")),
            new ViewContract(
                "dr_saturn", "EnemyView_DrSaturn.prefab",
                Array.Empty<StateBinding>(),
                new[]
                {
                    Trigger(EnemyAnimationCue.UtilityWindup, "Windup"),
                    Trigger(EnemyAnimationCue.UtilityRecovery, "Recover"),
                    Trigger(EnemyAnimationCue.Hit, "Hit"),
                    Trigger(EnemyAnimationCue.Death, "Death"),
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
                    State(EnemyAnimationCue.GlideWindup, "Fly_Start"),
                    State(EnemyAnimationCue.GlideActive, "Fly_Loop"),
                    State(EnemyAnimationCue.GlideRecovery, "Fly_Done"),
                },
                Array.Empty<TriggerBinding>(),
                Array.Empty<ParameterBinding>()),
        };

        private static readonly IReadOnlyDictionary<string, string> ExpectedTriggerDestinationMotions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TriggerKey("black_eye", EnemyAnimationCue.Hit)] = "<null>",
                [TriggerKey("black_eye", EnemyAnimationCue.Death)] =
                    "4d048268569f7314ca6e0610d38303f9:-1493303027246044120",
                [TriggerKey("startis", EnemyAnimationCue.Hit)] =
                    "f0cbedca08e0fed419ae5e7a60d0ac4f:7400000",
                [TriggerKey("startis", EnemyAnimationCue.Death)] =
                    "4a558bdb49c251a40ac087a31f95c2ac:9067093048684652814",
                [TriggerKey("rocket_face", EnemyAnimationCue.Hit)] = "<null>",
                [TriggerKey("rocket_face", EnemyAnimationCue.Death)] =
                    "1716406119d8be34d841f0d4eb033a2c:-5059006814000262888",
                [TriggerKey("astreton", EnemyAnimationCue.ActionExecute)] =
                    "9870afb7c6d615c458c88e0e341207e3:9067093048684652814",
                [TriggerKey("astreton", EnemyAnimationCue.Hit)] =
                    "9870afb7c6d615c458c88e0e341207e3:9067093048684652814",
                [TriggerKey("astreton", EnemyAnimationCue.Death)] =
                    "9870afb7c6d615c458c88e0e341207e3:-8481709605947105597",
                [TriggerKey("dr_saturn", EnemyAnimationCue.UtilityWindup)] =
                    "19cc367ffcf133f42b0c772ae30ed0b1:7400000",
                [TriggerKey("dr_saturn", EnemyAnimationCue.UtilityRecovery)] =
                    "c0977b05374049f46a273f93a78ed85e:7400000",
                [TriggerKey("dr_saturn", EnemyAnimationCue.Hit)] =
                    "f0cbedca08e0fed419ae5e7a60d0ac4f:7400000",
                [TriggerKey("dr_saturn", EnemyAnimationCue.Death)] =
                    "53bc3845eefb86f408fcb05d70cdf7b0:-5059006814000262888",
                [TriggerKey("j_peter", EnemyAnimationCue.Hit)] = "<null>",
                [TriggerKey("j_peter", EnemyAnimationCue.Death)] = "<null>",
                [TriggerKey("sunwheel", EnemyAnimationCue.Hit)] =
                    "bbf461ea260c63b4785c6c2c72a480bf:1827226128182048838",
                [TriggerKey("sunwheel", EnemyAnimationCue.Death)] =
                    "bbf461ea260c63b4785c6c2c72a480bf:9067093048684652814",
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
            }

            AssertEmpty(diagnostics, "Campaign production active Animator trigger violations");
        }

        [Test]
        public void CampaignMainEnemyViews_TriggerBindingsHaveExactLayerZeroDestinations()
        {
            var diagnostics = new List<string>();
            Assert.That(ExpectedTriggerDestinationMotions.Count,
                Is.EqualTo(ProductionContracts.Sum(contract => contract.ActiveTriggers.Length)),
                "Every production trigger cue must have one pinned effective destination Motion identity.");
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
                    ValidateTriggerDestination(contract, context, graph, binding, diagnostics);
                }
            }

            AssertEmpty(diagnostics, "Campaign production trigger destination violations");
        }

        [Test]
        public void CampaignMainEnemyViews_HaveExactBindingDispositionAndNoLegacyTimingAuthoring()
        {
            var diagnostics = new List<string>();
            var entriesById = IndexEntries(LoadCatalog(), diagnostics);
            foreach (var contract in ProductionContracts)
            {
                if (!TryGetContext(contract, entriesById, diagnostics, out var context))
                {
                    continue;
                }

                var authorings = context.Driver.GetComponentsInChildren<EnemyAnimationBindingAuthoring>(true);
                var expectedCount = contract.HasBinding ? 1 : 0;
                if (authorings.Length != expectedCount)
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), nameof(EnemyAnimationBindingAuthoring),
                        $"expected {expectedCount} root binding component(s), but found {authorings.Length}."));
                }

                if (context.Driver.GetComponent<EnemyAnimationTimingAuthoring>() != null)
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), nameof(EnemyAnimationTimingAuthoring),
                        "production View must not retain legacy timing authoring."));
                }
            }

            AssertEmpty(diagnostics, "Campaign production sparse binding disposition violations");
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

            EnemyAnimationBindingSnapshot snapshot = null;
            var bindingAuthoring = driver.GetComponent<EnemyAnimationBindingAuthoring>();
            if (contract.HasBinding)
            {
                if (bindingAuthoring == null)
                {
                    diagnostics.Add(Format(contract, prefabPath, DescribeController(animator.runtimeAnimatorController),
                        nameof(EnemyAnimationBindingAuthoring), "expected root sparse binding is missing."));
                    return false;
                }

                try
                {
                    snapshot = bindingAuthoring.CreateSnapshot();
                }
                catch (Exception exception)
                {
                    diagnostics.Add(Format(contract, prefabPath, DescribeController(animator.runtimeAnimatorController),
                        nameof(EnemyAnimationBindingAuthoring), $"snapshot is invalid: {exception.Message}"));
                    return false;
                }
            }
            else if (bindingAuthoring != null)
            {
                diagnostics.Add(Format(contract, prefabPath, DescribeController(animator.runtimeAnimatorController),
                    nameof(EnemyAnimationBindingAuthoring), "ApprovedNoBinding View must not have a binding."));
                return false;
            }

            context = new EntryContext(driver, animator, animator.runtimeAnimatorController, snapshot);
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
            var transitions = new List<TransitionDescriptor>();
            for (var layerIndex = 0; layerIndex < controller.layers.Length; layerIndex++)
            {
                var layer = controller.layers[layerIndex];
                CollectGraph(layer.stateMachine, layer.name, layerIndex, states, conditions, transitions);
            }

            return new ControllerGraph(states, conditions, transitions);
        }

        private static void CollectGraph(
            AnimatorStateMachine stateMachine,
            string stateMachinePath,
            int layerIndex,
            ICollection<StateDescriptor> states,
            ISet<string> conditions,
            ICollection<TransitionDescriptor> transitions)
        {
            foreach (var childState in stateMachine.states)
            {
                var statePath = stateMachinePath + "." + childState.state.name;
                states.Add(new StateDescriptor(childState.state, statePath, layerIndex));
                CollectTransitions(childState.state.transitions, statePath, layerIndex, conditions, transitions);
            }

            CollectTransitions(stateMachine.anyStateTransitions, stateMachinePath + ".<AnyState>", layerIndex,
                conditions, transitions);
            CollectTransitions(stateMachine.entryTransitions, stateMachinePath + ".<Entry>", layerIndex,
                conditions, transitions);
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                var childPath = stateMachinePath + "." + childStateMachine.stateMachine.name;
                CollectTransitions(stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine),
                    childPath, layerIndex, conditions, transitions);
                CollectGraph(childStateMachine.stateMachine,
                    childPath, layerIndex, states, conditions, transitions);
            }
        }

        private static void CollectTransitions<T>(
            IEnumerable<T> authoredTransitions,
            string sourcePath,
            int layerIndex,
            ISet<string> conditions,
            ICollection<TransitionDescriptor> descriptors)
            where T : AnimatorTransitionBase
        {
            var transitionSet = authoredTransitions.Where(transition => transition != null).ToArray();
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

                var destinationName = transition.isExit
                    ? "<Exit>"
                    : transition.destinationState != null
                        ? transition.destinationState.name
                        : transition.destinationStateMachine != null
                            ? "<StateMachine:" + transition.destinationStateMachine.name + ">"
                            : "<null>";
                descriptors.Add(new TransitionDescriptor(
                    sourcePath,
                    destinationName,
                    layerIndex,
                    transition.conditions.ToArray()));
            }
        }

        private static void ValidateTriggerDestination(
            ViewContract contract,
            EntryContext context,
            ControllerGraph graph,
            TriggerBinding binding,
            ICollection<string> diagnostics)
        {
            var matches = graph.Transitions.Where(transition =>
                    transition.Conditions.Any(condition =>
                        string.Equals(condition.parameter, binding.ExpectedName, StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length == 0)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    $"trigger '{binding.ExpectedName}' has no executable transition."));
                return;
            }

            foreach (var transition in matches)
            {
                if (transition.LayerIndex != 0 ||
                    !string.Equals(transition.DestinationName, binding.ExpectedDestinationName,
                        StringComparison.Ordinal))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
                        $"trigger '{binding.ExpectedName}' transition '{transition.SourcePath}' must target " +
                        $"layer 0 state '{binding.ExpectedDestinationName}', but targets layer " +
                        $"{transition.LayerIndex} '{transition.DestinationName}'."));
                }

                foreach (var condition in transition.Conditions.Where(condition =>
                             string.Equals(condition.parameter, binding.ExpectedName, StringComparison.Ordinal)))
                {
                    if (condition.mode != AnimatorConditionMode.If || Math.Abs(condition.threshold) > 0.000001f)
                    {
                        diagnostics.Add(Format(contract, contract.PrefabPath,
                            DescribeController(context.AssignedController), binding.Cue.ToString(),
                            $"trigger '{binding.ExpectedName}' must use If/0, but uses " +
                            $"{condition.mode}/{condition.threshold:R}."));
                    }
                }
            }

            var destinationStates = graph.States.Where(state => state.LayerIndex == 0 &&
                string.Equals(state.State.name, binding.ExpectedDestinationName, StringComparison.Ordinal)).ToArray();
            if (destinationStates.Length != 1)
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    $"expected exactly one layer 0 destination state '{binding.ExpectedDestinationName}', " +
                    $"but found {destinationStates.Length}."));
                return;
            }

            var motion = ResolveEffectiveMotion(context.AssignedController, destinationStates[0].State.motion);
            var identity = motion == null ? "<null>" : DescribeAssetIdentity(motion);
            var key = TriggerKey(contract.PresentationId, binding.Cue);
            if (!ExpectedTriggerDestinationMotions.TryGetValue(key, out var expectedIdentity))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    "effective destination Motion identity is not pinned."));
            }
            else if (!string.Equals(identity, expectedIdentity, StringComparison.Ordinal))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    $"effective destination Motion expected '{expectedIdentity}', but is '{identity}'."));
            }

            TestContext.WriteLine(
                $"TRIGGER_DESTINATION|{contract.PresentationId}|{binding.Cue}|{binding.ExpectedName}|" +
                $"{destinationStates[0].FullPath}|{identity}");
        }

        private static string DescribeAssetIdentity(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return "<null>";
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localFileId)
                ? guid + ":" + localFileId
                : asset.GetType().Name + ":" + asset.name;
        }

        private static string TriggerKey(string presentationId, EnemyAnimationCue cue)
        {
            return presentationId + "|" + cue;
        }

        private static void ValidateActiveStates(
            ViewContract contract,
            EntryContext context,
            ControllerGraph graph,
            ICollection<string> diagnostics)
        {
            foreach (var binding in contract.ActiveStates)
            {
                if (context.Binding == null ||
                    !context.Binding.TryGetBinding(binding.Cue, out var runtimeBinding))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
                        "expected cue binding is missing."));
                    continue;
                }

                var stateName = runtimeBinding.TargetName;
                if (runtimeBinding.PrimaryDispatchMode != EnemyAnimationDispatchMode.State ||
                    !string.Equals(stateName, binding.ExpectedName, StringComparison.Ordinal))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
                        $"expected State target '{binding.ExpectedName}', but is " +
                        $"'{runtimeBinding.PrimaryDispatchMode}:{stateName}'."));
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
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
                        $"active state '{stateName}' {detail}."));
                    continue;
                }

                if (!IsDriverReachable(context.AssignedController, stateName))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
                        $"state '{matches[0].FullPath}' is not reachable by the driver's supported layer-0 hashes."));
                }

                if (!HasEffectiveMotion(context.AssignedController, matches[0].State.motion))
                {
                    diagnostics.Add(Format(contract, contract.PrefabPath,
                        DescribeController(context.AssignedController), binding.Cue.ToString(),
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

        private static Motion ResolveEffectiveMotion(RuntimeAnimatorController assigned, Motion baseMotion)
        {
            if (!(assigned is AnimatorOverrideController overrideController) ||
                !(baseMotion is AnimationClip baseClip))
            {
                return baseMotion;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            var overridePair = overrides.FirstOrDefault(pair => pair.Key == baseClip);
            return overridePair.Key != null && overridePair.Value != null ? overridePair.Value : baseClip;
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
            if (contract.ActiveStates.Length == 0)
            {
                return;
            }

            if (context.Binding == null ||
                !EnemyAnimationTimingAuthoring.IsStateTransitionCrossFadeOverride(
                    context.Binding.DefaultStateCrossFadeDurationSeconds))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController),
                    nameof(EnemyAnimationBindingSnapshot.DefaultStateCrossFadeDurationSeconds),
                    "direct-state dispatch requires a nonnegative binding cross-fade override."));
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
            if (context.Binding == null ||
                !context.Binding.TryGetBinding(binding.Cue, out var runtimeBinding))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    "expected cue binding is missing."));
                return;
            }

            var triggerName = runtimeBinding.TargetName;
            if (runtimeBinding.PrimaryDispatchMode != EnemyAnimationDispatchMode.Trigger ||
                !string.Equals(triggerName, binding.ExpectedName, StringComparison.Ordinal))
            {
                diagnostics.Add(Format(contract, contract.PrefabPath,
                    DescribeController(context.AssignedController), binding.Cue.ToString(),
                    $"expected Trigger target '{binding.ExpectedName}', but is " +
                    $"'{runtimeBinding.PrimaryDispatchMode}:{triggerName}'."));
                return;
            }

            ValidateAuthoredTrigger(
                contract, context, controller, graph, binding.Cue.ToString(), triggerName, diagnostics);
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
                Trigger(EnemyAnimationCue.Hit, "Hit"),
                Trigger(EnemyAnimationCue.Death, "Death"),
            };
        }

        private static StateBinding State(EnemyAnimationCue cue, string targetName)
        {
            return new StateBinding(cue, targetName);
        }

        private static TriggerBinding Trigger(
            EnemyAnimationCue cue,
            string targetName,
            string destinationName = null)
        {
            return new TriggerBinding(cue, targetName, destinationName ?? targetName);
        }

        private static ParameterBinding[] Parameters(params string[] names)
        {
            return names.Select(name => DriverParameters.Single(binding =>
                string.Equals(binding.Name, name, StringComparison.Ordinal))).ToArray();
        }

        private readonly struct EntryContext
        {
            public EntryContext(
                EnemyAnimatorDriver driver,
                Animator animator,
                RuntimeAnimatorController assignedController,
                EnemyAnimationBindingSnapshot binding)
            {
                Driver = driver;
                Animator = animator;
                AssignedController = assignedController;
                Binding = binding;
            }
            public EnemyAnimatorDriver Driver { get; }
            public Animator Animator { get; }
            public RuntimeAnimatorController AssignedController { get; }
            public EnemyAnimationBindingSnapshot Binding { get; }
        }

        private sealed class ViewContract
        {
            public ViewContract(
                string presentationId,
                string prefabName,
                StateBinding[] activeStates,
                TriggerBinding[] activeTriggers,
                ParameterBinding[] requiredParameters)
            {
                PresentationId = presentationId;
                PrefabPath = ProductionPrefabRoot + "/" + prefabName;
                ActiveStates = activeStates;
                ActiveTriggers = activeTriggers;
                RequiredParameters = requiredParameters;
            }
            public string PresentationId { get; }
            public string PrefabPath { get; }
            public StateBinding[] ActiveStates { get; }
            public TriggerBinding[] ActiveTriggers { get; }
            public ParameterBinding[] RequiredParameters { get; }
            public bool HasBinding => ActiveStates.Length + ActiveTriggers.Length > 0;
        }

        private readonly struct StateBinding
        {
            public StateBinding(EnemyAnimationCue cue, string expectedName)
            {
                Cue = cue;
                ExpectedName = expectedName;
            }
            public EnemyAnimationCue Cue { get; }
            public string ExpectedName { get; }
        }

        private readonly struct TriggerBinding
        {
            public TriggerBinding(
                EnemyAnimationCue cue,
                string expectedName,
                string expectedDestinationName)
            {
                Cue = cue;
                ExpectedName = expectedName;
                ExpectedDestinationName = expectedDestinationName;
            }
            public EnemyAnimationCue Cue { get; }
            public string ExpectedName { get; }
            public string ExpectedDestinationName { get; }
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

        private readonly struct TransitionDescriptor
        {
            public TransitionDescriptor(
                string sourcePath,
                string destinationName,
                int layerIndex,
                AnimatorCondition[] conditions)
            {
                SourcePath = sourcePath;
                DestinationName = destinationName;
                LayerIndex = layerIndex;
                Conditions = conditions;
            }
            public string SourcePath { get; }
            public string DestinationName { get; }
            public int LayerIndex { get; }
            public AnimatorCondition[] Conditions { get; }
        }

        private sealed class ControllerGraph
        {
            public ControllerGraph(
                IReadOnlyList<StateDescriptor> states,
                IReadOnlyCollection<string> consumedConditions,
                IReadOnlyList<TransitionDescriptor> transitions)
            {
                States = states;
                ConsumedConditions = consumedConditions;
                Transitions = transitions;
            }
            public IReadOnlyList<StateDescriptor> States { get; }
            public IReadOnlyCollection<string> ConsumedConditions { get; }
            public IReadOnlyList<TransitionDescriptor> Transitions { get; }
        }
    }
}
