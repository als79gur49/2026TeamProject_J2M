using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TopologySceneBindingSmokeCoreTests
    {
        private static readonly string[] ProductionScenePaths =
        {
            "Assets/Scenes/UIAudioScene.unity",
        };

        [Test]
        [Category("Core")]
        public void TopologySceneBindings_AllProductionScenes_ValidateBridgeBindings()
        {
            var failures = new List<string>();
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                try
                {
                    var rootObjects = scene.GetRootGameObjects();
                    Assert.That(
                        CountComponentsInScene<GameplaySceneHost>(rootObjects),
                        Is.EqualTo(1),
                        scenePath);

                    foreach (var controller in FindInScene<TopologyVisualBridgeVisibilityController>(rootObjects))
                    {
                        ValidateBridgeController(scenePath, controller, failures);
                    }
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            Assert.That(failures, Is.Empty, "Topology visual bridge scene binding failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void TopologySceneBindings_NoBridgeColliderOrGameplayOccupancyMeaning()
        {
            var failures = new List<string>();
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                try
                {
                    foreach (var controller in FindInScene<TopologyVisualBridgeVisibilityController>(scene.GetRootGameObjects()))
                    {
                        foreach (var binding in controller.Bindings)
                        {
                            var target = binding?.TargetObject;
                            if (target == null)
                            {
                                continue;
                            }

                            if (target.GetComponentsInChildren<Collider>(true).Length > 0 ||
                                target.GetComponentsInChildren<Collider2D>(true).Length > 0)
                            {
                                failures.Add($"{scenePath}: bridge target '{GetPath(target.transform)}' has a Collider/Collider2D. Bridge targets must remain presentation-only.");
                            }
                        }
                    }
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            Assert.That(failures, Is.Empty, "Topology visual bridge collider failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void TopologyPostFx_ActualSceneBindings_RuntimeCloneSourceProfileAssigned()
        {
            var failures = new List<string>();
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                try
                {
                    var authoring = FindInScene<GameplayCameraTopologyAuthoring>(scene.GetRootGameObjects()).SingleOrDefault();
                    if (authoring == null)
                    {
                        failures.Add($"{scenePath}: missing {nameof(GameplayCameraTopologyAuthoring)}.");
                        continue;
                    }

                    authoring.Validate();
                    var serializedAuthoring = new SerializedObject(authoring);
                    var sourceMode = serializedAuthoring.FindProperty("sourceMode");
                    var preset = serializedAuthoring.FindProperty("preset");
                    Assert.That(sourceMode, Is.Not.Null, scenePath);
                    Assert.That(preset, Is.Not.Null, scenePath);
                    Assert.That(preset.objectReferenceValue, Is.Not.Null, scenePath);

                    var serializedPreset = new SerializedObject(preset.objectReferenceValue);
                    var sharedTuning = serializedPreset.FindProperty("sharedTuning");
                    var postFxProfile = sharedTuning?.FindPropertyRelative("topologyTransitionPostFxProfile");
                    var authoritativeVolumeProfile = postFxProfile?.FindPropertyRelative("authoritativeVolumeProfile");
                    Assert.That(authoritativeVolumeProfile, Is.Not.Null, scenePath);
                    Assert.That(authoritativeVolumeProfile.objectReferenceValue, Is.Not.Null, scenePath);
                }
                catch (Exception exception)
                {
                    failures.Add($"{scenePath}: {exception.GetType().Name}: {exception.Message}");
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            Assert.That(failures, Is.Empty, "Topology post-fx actual scene binding failures:\n" + string.Join("\n", failures));
        }

        [Test]
        [Category("Core")]
        public void TopologyCameraRig_ActualSceneBindings_ConsumesPresentationStateOnly()
        {
            foreach (var scenePath in ProductionScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                try
                {
                    var authoring = FindInScene<GameplayCameraTopologyAuthoring>(scene.GetRootGameObjects()).SingleOrDefault();
                    Assert.That(authoring, Is.Not.Null, scenePath);
                    Assert.DoesNotThrow(() => authoring.CreateSnapshot(), scenePath);
                }
                finally
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }

            var cameraRigSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs");
            Assert.That(cameraRigSource, Does.Contain("IGameplayCameraAdditivePosePort"));
            Assert.That(cameraRigSource, Does.Contain("ApplyAdditivePose"));
            Assert.That(cameraRigSource, Does.Contain("ResetAdditivePose"));
            Assert.That(cameraRigSource, Does.Not.Contain("TopologyTransitionVisualState"));
            Assert.That(cameraRigSource, Does.Not.Contain("WorldState"));
            Assert.That(cameraRigSource, Does.Not.Contain("TickPipeline"));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_DoesNotAffectDeterminismHash()
        {
            var hashBuilderSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs");

            Assert.That(hashBuilderSource, Does.Not.Contain("TopologyTransitionVisualState"));
            Assert.That(hashBuilderSource, Does.Not.Contain("GameplayCameraRig"));
            Assert.That(hashBuilderSource, Does.Not.Contain("TopologyTransitionPostFx"));
        }

        [Test]
        [Category("Core")]
        public void TopologyPresenter_DoesNotMutateCubeTopologyState()
        {
            var presentationOnlySources = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTopologyTransitionController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyVisualBridgeVisibilityController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PostFx/Controllers/TopologyTransitionPostFxController.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs",
            };

            foreach (var sourcePath in presentationOnlySources)
            {
                var source = ReadRepoFile(sourcePath);
                Assert.That(source, Does.Not.Contain("WorldState"), sourcePath);
                Assert.That(source, Does.Not.Contain("SetTopology"), sourcePath);
            }
        }

        private static void ValidateBridgeController(
            string scenePath,
            TopologyVisualBridgeVisibilityController controller,
            ICollection<string> failures)
        {
            var seenTargets = new HashSet<GameObject>();
            var targets = new List<GameObject>();
            foreach (var binding in controller.Bindings)
            {
                if (binding == null)
                {
                    failures.Add($"{scenePath}: {GetPath(controller.transform)} contains a null bridge binding.");
                    continue;
                }

                var target = binding.TargetObject;
                if (target == null)
                {
                    failures.Add($"{scenePath}: {GetPath(controller.transform)} contains a bridge binding with no target.");
                    continue;
                }

                if (binding.FirstFace == binding.SecondFace)
                {
                    failures.Add($"{scenePath}: bridge target '{GetPath(target.transform)}' binds the same face twice.");
                }

                if (!seenTargets.Add(target))
                {
                    failures.Add($"{scenePath}: bridge target '{GetPath(target.transform)}' is duplicated.");
                }

                if (controller.transform.IsChildOf(target.transform))
                {
                    failures.Add($"{scenePath}: bridge target '{GetPath(target.transform)}' is the controller root or an ancestor.");
                }

                foreach (var existing in targets)
                {
                    if (target.transform.IsChildOf(existing.transform) ||
                        existing.transform.IsChildOf(target.transform))
                    {
                        failures.Add($"{scenePath}: bridge target '{GetPath(target.transform)}' overlaps hierarchy with '{GetPath(existing.transform)}'.");
                    }
                }

                targets.Add(target);
            }
        }

        private static T[] FindInScene<T>(IEnumerable<GameObject> rootObjects) where T : Component
        {
            return rootObjects
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static int CountComponentsInScene<T>(IEnumerable<GameObject> rootObjects) where T : Component
        {
            return rootObjects.Sum(root => root.GetComponentsInChildren<T>(true).Length);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, relativePath));
        }

        private static string GetPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }
}
