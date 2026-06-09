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
        public void KeyboardBindingSettingsService_UsesCanonicalActionPaths()
        {
            Assert.That(GameplayInputActionPaths.PlayerMove, Is.EqualTo("Player/Move"));
            Assert.That(GameplayInputActionPaths.PlayerPush, Is.EqualTo("Player/Push"));
            Assert.That(GameplayInputActionPaths.PlayerFlip, Is.EqualTo("Player/Flip"));
            Assert.That(GameplayInputActionPaths.UiNavigate, Is.EqualTo("UI/Navigate"));
            Assert.That(KeyboardBindingSettingsService.MoveActionPath, Is.EqualTo(GameplayInputActionPaths.PlayerMove));
            Assert.That(KeyboardBindingSettingsService.NavigateActionPath, Is.EqualTo(GameplayInputActionPaths.UiNavigate));
            Assert.That(KeyboardBindingSettingsService.PushActionPath, Is.EqualTo(GameplayInputActionPaths.PlayerPush));
            Assert.That(KeyboardBindingSettingsService.FlipActionPath, Is.EqualTo(GameplayInputActionPaths.PlayerFlip));
        }

        [TestCase(nameof(GameplayInputActionPaths.PlayerMove))]
        [TestCase(nameof(GameplayInputActionPaths.PlayerPush))]
        [TestCase(nameof(GameplayInputActionPaths.PlayerFlip))]
        [TestCase(nameof(GameplayInputActionPaths.UiNavigate))]
        public void KeyboardBindingSettingsService_MissingRequiredAction_ThrowsSetupDefect(string missingPathName)
        {
            var missingPath = ResolveCanonicalPath(missingPathName);
            var actions = CreateActions(
                includeMove: missingPath != GameplayInputActionPaths.PlayerMove,
                includePush: missingPath != GameplayInputActionPaths.PlayerPush,
                includeFlip: missingPath != GameplayInputActionPaths.PlayerFlip,
                includeUiNavigate: missingPath != GameplayInputActionPaths.UiNavigate);

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                using var service = new KeyboardBindingSettingsService(actions, new FakeKeyboardBindingStore());
            });

            Assert.That(exception.Message, Does.Contain(missingPath));
            Assert.That(exception.Message, Does.Contain("Required input action"));
        }

        [Test]
        public void KeyboardBindingSettingsService_PushRequiresKeyboardBinding()
        {
            var actions = CreateActions(pushKeyboardBinding: false, pushGamepadBinding: true);

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                using var service = new KeyboardBindingSettingsService(actions, new FakeKeyboardBindingStore());
            });

            Assert.That(exception.Message, Does.Contain(GameplayInputActionPaths.PlayerPush));
            Assert.That(exception.Message, Does.Contain("Required keyboard binding"));
        }

        [Test]
        public void KeyboardBindingSettingsService_FlipRequiresKeyboardBinding()
        {
            var actions = CreateActions(flipKeyboardBinding: false);

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                using var service = new KeyboardBindingSettingsService(actions, new FakeKeyboardBindingStore());
            });

            Assert.That(exception.Message, Does.Contain(GameplayInputActionPaths.PlayerFlip));
            Assert.That(exception.Message, Does.Contain("Required keyboard binding"));
        }

        [Test]
        public void KeyboardBindingSettingsService_PushFlipKeyboardBindingsPresent_AllowsSetup()
        {
            var actions = CreateActions();

            using var service = new KeyboardBindingSettingsService(actions, new FakeKeyboardBindingStore());

            var snapshot = service.Read();
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
            Assert.That(IsEffective(actions, GameplayInputActionPaths.PlayerMove, "<Keyboard>/w"), Is.False);
            Assert.That(IsEffective(actions, GameplayInputActionPaths.PlayerMove, "<Keyboard>/upArrow"), Is.True);
        }

        [Test]
        public void KeyboardBindingSettingsService_Wasd_DisablesUiNavigateArrowKeys()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.Wasd);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            Assert.That(IsEffective(actions, GameplayInputActionPaths.UiNavigate, "<Keyboard>/w"), Is.True);
            Assert.That(IsEffective(actions, GameplayInputActionPaths.UiNavigate, "<Keyboard>/upArrow"), Is.False);
        }

        [Test]
        public void KeyboardBindingSettingsService_ArrowKeys_DisablesUiNavigateWasd()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            Assert.That(IsEffective(actions, GameplayInputActionPaths.UiNavigate, "<Keyboard>/w"), Is.False);
            Assert.That(IsEffective(actions, GameplayInputActionPaths.UiNavigate, "<Keyboard>/upArrow"), Is.True);
        }

        [Test]
        public void KeyboardBindingSettingsService_ResetToDefaults_RestoresWasdPushEFlipQ()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                MovementScheme = KeyboardMovementScheme.ArrowKeys,
                BindingOverridesJson = BuildOverridesJson(actions, (GameplayInputActionPaths.PlayerPush, "<Keyboard>/r")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);

            var snapshot = service.ResetToDefaults();

            Assert.That(snapshot.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("E"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("Q"));
            Assert.That(store.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(store.BindingOverridesJson, Is.Null);
            Assert.That(IsEffective(actions, GameplayInputActionPaths.PlayerMove, "<Keyboard>/w"), Is.True);
            Assert.That(IsEffective(actions, GameplayInputActionPaths.PlayerMove, "<Keyboard>/upArrow"), Is.False);
        }

        [Test]
        public void KeyboardBindingSettingsService_LoadsBindingOverridesFromStore()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore
            {
                BindingOverridesJson = BuildOverridesJson(
                    actions,
                    (GameplayInputActionPaths.PlayerPush, "<Keyboard>/r"),
                    (GameplayInputActionPaths.PlayerFlip, "<Keyboard>/t")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);
            var snapshot = service.Read();

            Assert.That(snapshot.PushDisplayName, Is.EqualTo("R"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("T"));
        }

        [Test]
        public void KeyboardBindingSettingsService_RestoresPushFlipOverrides_FromSavedSettingsClone()
        {
            var sourceActions = CreateActions();
            var clonedActions = CloneActions(sourceActions);
            var store = new FakeKeyboardBindingStore
            {
                BindingOverridesJson = BuildOverridesJson(
                    sourceActions,
                    (GameplayInputActionPaths.PlayerPush, "<Keyboard>/r"),
                    (GameplayInputActionPaths.PlayerFlip, "<Keyboard>/t")),
            };

            using var service = new KeyboardBindingSettingsService(clonedActions, store);
            var snapshot = service.Read();

            Assert.That(snapshot.PushDisplayName, Is.EqualTo("R"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("T"));
            Assert.That(HasEffectivePath(clonedActions, GameplayInputActionPaths.PlayerPush, "<Keyboard>/r"), Is.True);
            Assert.That(HasEffectivePath(clonedActions, GameplayInputActionPaths.PlayerFlip, "<Keyboard>/t"), Is.True);
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
                BindingOverridesJson = BuildOverridesJson(actions, (GameplayInputActionPaths.PlayerPush, "<Keyboard>/upArrow")),
            };

            using var service = new KeyboardBindingSettingsService(actions, store);

            var result = service.SetMovementScheme(KeyboardMovementScheme.ArrowKeys);

            Assert.That(result, Is.EqualTo(KeyboardBindingValidationStatus.MovementConflict));
            Assert.That(service.Read().MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
        }

        private InputActionAsset CreateActions(
            bool includeMove = true,
            bool includePush = true,
            bool includeFlip = true,
            bool includeUiNavigate = true,
            bool pushKeyboardBinding = true,
            bool pushGamepadBinding = false,
            bool flipKeyboardBinding = true)
        {
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var player = new InputActionMap(GameplayInputActionPaths.PlayerActionMap);
            if (includeMove)
            {
                var move = player.AddAction(GameplayInputActionPaths.MoveAction, InputActionType.Value);
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
            }

            if (includePush)
            {
                var push = player.AddAction(GameplayInputActionPaths.PushAction, InputActionType.Button);
                if (pushKeyboardBinding)
                {
                    push.AddBinding("<Keyboard>/e")
                        .WithGroup("Keyboard&Mouse");
                }

                if (pushGamepadBinding)
                {
                    push.AddBinding("<Gamepad>/buttonNorth")
                        .WithGroup("Gamepad");
                }
            }

            if (includeFlip)
            {
                var flip = player.AddAction(GameplayInputActionPaths.FlipAction, InputActionType.Button);
                if (flipKeyboardBinding)
                {
                    flip.AddBinding("<Keyboard>/q")
                        .WithGroup("Keyboard&Mouse");
                }
            }

            actions.AddActionMap(player);

            var ui = new InputActionMap(GameplayInputActionPaths.UiActionMap);
            if (includeUiNavigate)
            {
                var navigate = ui.AddAction(GameplayInputActionPaths.NavigateAction, InputActionType.PassThrough);
                navigate.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/rightArrow");
            }

            actions.AddActionMap(ui);

            _createdActions.Add(actions);
            return actions;
        }

        private InputActionAsset CloneActions(InputActionAsset sourceActions)
        {
            var clone = InputActionAsset.FromJson(sourceActions.ToJson());
            _createdActions.Add(clone);
            return clone;
        }

        private static string ResolveCanonicalPath(string pathName)
        {
            switch (pathName)
            {
                case nameof(GameplayInputActionPaths.PlayerMove):
                    return GameplayInputActionPaths.PlayerMove;
                case nameof(GameplayInputActionPaths.PlayerPush):
                    return GameplayInputActionPaths.PlayerPush;
                case nameof(GameplayInputActionPaths.PlayerFlip):
                    return GameplayInputActionPaths.PlayerFlip;
                case nameof(GameplayInputActionPaths.UiNavigate):
                    return GameplayInputActionPaths.UiNavigate;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pathName), pathName, "Unknown canonical path name.");
            }
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

        private static bool HasEffectivePath(InputActionAsset actions, string actionPath, string bindingPath)
        {
            var action = actions.FindAction(actionPath);
            return action.bindings
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
