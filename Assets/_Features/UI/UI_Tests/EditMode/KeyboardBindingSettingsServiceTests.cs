using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.UI.Tests
{
    public sealed class KeyboardBindingSettingsServiceTests
    {
        private readonly List<InputActionAsset> _createdActions = new List<InputActionAsset>();

        [TearDown]
        public void TearDown()
        {
            foreach (var actions in _createdActions)
            {
                UnityEngine.Object.DestroyImmediate(actions);
            }

            _createdActions.Clear();
        }

        [Test]
        public void KeyboardBindingSettingsService_DefaultSnapshot_IsWasdPushEFlipQ()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var snapshot = service.Read();

            Assert.That(snapshot.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(snapshot.MovementDisplayName, Is.EqualTo("WASD"));
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("E"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("Q"));
        }

        [Test]
        public void KeyboardBindingSettingsService_SetMovementScheme_StoresAndAppliesArrowKeys()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            Assert.That(store.MovementScheme, Is.EqualTo(KeyboardMovementScheme.ArrowKeys));
            Assert.That(IsEffective(actions, "Player/Move", "<Keyboard>/w"), Is.False);
            Assert.That(IsEffective(actions, "Player/Move", "<Keyboard>/upArrow"), Is.True);
        }

        [Test]
        public void KeyboardBindingSettingsService_Wasd_DisablesUiNavigateArrowKeys()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.Wasd);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            Assert.That(IsEffective(actions, "UI/Navigate", "<Keyboard>/w"), Is.True);
            Assert.That(IsEffective(actions, "UI/Navigate", "<Keyboard>/upArrow"), Is.False);
        }

        [Test]
        public void KeyboardBindingSettingsService_ArrowKeys_DisablesUiNavigateWasd()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            Assert.That(IsEffective(actions, "UI/Navigate", "<Keyboard>/w"), Is.False);
            Assert.That(IsEffective(actions, "UI/Navigate", "<Keyboard>/upArrow"), Is.True);
        }

        [Test]
        public void KeyboardBindingSettingsService_ResetToDefaults_RestoresWasdPushEFlipQ()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                MovementScheme = KeyboardMovementScheme.ArrowKeys,
                BindingOverridesJson = BuildOverridesJson(actions, ("Player/Push", "<Keyboard>/r")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);

            var snapshot = service.ResetToDefaults();

            Assert.That(snapshot.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("E"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("Q"));
            Assert.That(store.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(store.BindingOverridesJson, Is.Null);
            Assert.That(IsEffective(actions, "Player/Move", "<Keyboard>/w"), Is.True);
            Assert.That(IsEffective(actions, "Player/Move", "<Keyboard>/upArrow"), Is.False);
        }

        [Test]
        public void KeyboardBindingSettingsService_LoadsBindingOverridesFromStore()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                BindingOverridesJson = BuildOverridesJson(
                    actions,
                    ("Player/Push", "<Keyboard>/r"),
                    ("Player/Flip", "<Keyboard>/t")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);
            var snapshot = service.Read();

            Assert.That(snapshot.PushDisplayName, Is.EqualTo("R"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("T"));
        }

        [Test]
        public void KeyboardBindingSettingsService_CorruptedOverrideJson_FallsBackSafely()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                BindingOverridesJson = "{not valid json",
            };

            using var service = new KeyboardBindingSettingsService(actions, store);
            var snapshot = service.Read();

            Assert.That(snapshot.PushDisplayName, Is.EqualTo("E"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("Q"));
            Assert.That(store.BindingOverridesJson, Is.Null);
            Assert.That(store.SaveCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void KeyboardBindingSettingsService_RejectsMovementSchemeChangeIfExistingActionKeyConflicts()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                BindingOverridesJson = BuildOverridesJson(actions, ("Player/Push", "<Keyboard>/upArrow")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.MovementConflict));
            Assert.That(service.Read().MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
        }

        private InputActionAsset CreateActions()
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = new InputActionMap("Player");
            var move = player.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            player.AddAction("Push", InputActionType.Button)
                .AddBinding("<Keyboard>/e")
                .WithGroup("Keyboard&Mouse");
            player.AddAction("Flip", InputActionType.Button)
                .AddBinding("<Keyboard>/q")
                .WithGroup("Keyboard&Mouse");
            actions.AddActionMap(player);

            var ui = new InputActionMap("UI");
            var navigate = ui.AddAction("Navigate", InputActionType.PassThrough);
            navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            actions.AddActionMap(ui);

            _createdActions.Add(actions);
            return actions;
        }

        private static string BuildOverridesJson(
            InputActionAsset actions,
            params (string ActionPath, string OverridePath)[] overrides)
        {
            var entries = overrides.Select(overrideEntry =>
            {
                var action = actions.FindAction(overrideEntry.ActionPath);
                var binding = action.bindings.First(candidate =>
                    !candidate.isComposite &&
                    !candidate.isPartOfComposite &&
                    candidate.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase));
                return
                    "{\"action\":\"" + overrideEntry.ActionPath + "\"," +
                    "\"id\":\"" + binding.id + "\"," +
                    "\"path\":\"" + overrideEntry.OverridePath + "\"," +
                    "\"interactions\":\"null\"," +
                    "\"processors\":\"null\"}";
            });

            return "{\"bindings\":[" + string.Join(",", entries) + "]}";
        }

        private static bool IsEffective(InputActionAsset actions, string actionPath, string bindingPath)
        {
            var action = actions.FindAction(actionPath);
            return action.bindings
                .Where(binding => string.Equals(binding.path, bindingPath, StringComparison.OrdinalIgnoreCase))
                .Any(binding => string.Equals(binding.effectivePath, bindingPath, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class FakeKeyboardBindingStore : IKeyboardBindingStore
        {
            public KeyboardMovementScheme? MovementScheme { get; set; }

            public string BindingOverridesJson { get; set; }

            public int SaveCount { get; private set; }

            public bool TryLoadMovementScheme(out KeyboardMovementScheme scheme)
            {
                scheme = MovementScheme ?? KeyboardMovementScheme.Wasd;
                return MovementScheme.HasValue;
            }

            public void SaveMovementScheme(KeyboardMovementScheme scheme)
            {
                MovementScheme = scheme;
            }

            public bool TryLoadBindingOverridesJson(out string json)
            {
                json = BindingOverridesJson;
                return !string.IsNullOrWhiteSpace(json);
            }

            public void SaveBindingOverridesJson(string json)
            {
                BindingOverridesJson = string.IsNullOrWhiteSpace(json) ? null : json;
            }

            public void ClearBindingOverridesJson()
            {
                BindingOverridesJson = null;
            }

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
