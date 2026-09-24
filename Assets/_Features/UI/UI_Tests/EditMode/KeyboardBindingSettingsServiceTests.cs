using System;
using System.Collections.Generic;
using System.Linq;
using Game.Shared.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Feature.UI.Tests
{
    public sealed class KeyboardBindingSettingsServiceTests
    {
        private const string ProductionActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string ProductionPushKeyboardBindingId = "1c04ea5f-b012-41d1-a6f7-02e963b52893";
        private const string ProductionFlipKeyboardBindingId = "f6403135-b3d4-4300-bf3e-9a0ae3dbec40";

        // Captured via production StartRebind on c9b765235 before removing the seven Player actions.
        private const string PreRemovalProductionOverridesJson = @"{""bindings"":[{""action"":""Player/Push"",""id"":""1c04ea5f-b012-41d1-a6f7-02e963b52893"",""path"":""<Keyboard>/r"",""interactions"":""null"",""processors"":""null""},{""action"":""Player/Flip"",""id"":""f6403135-b3d4-4300-bf3e-9a0ae3dbec40"",""path"":""<Keyboard>/t"",""interactions"":""null"",""processors"":""null""}]}";

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
        public void KeyboardBindingSettingsService_DefaultSnapshot_IsWasdPushJFlipK()
        {
            var actions = CreateActions();
            var store = new FakeKeyboardBindingStore();
            using var service = new KeyboardBindingSettingsService(actions, store);

            var snapshot = service.Read();

            Assert.That(snapshot.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("J"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("K"));
        }

        [Test]
        public void ProductionInputActions_DefaultKeyboardBindings_ArePushJFlipK_WithPersistentBindingIds()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProductionActionsPath);

            Assert.That(actions, Is.Not.Null, ProductionActionsPath);
            AssertProductionKeyboardBinding(
                actions,
                GameplayInputActionPaths.PlayerPush,
                "<Keyboard>/j",
                ProductionPushKeyboardBindingId);
            AssertProductionKeyboardBinding(
                actions,
                GameplayInputActionPaths.PlayerFlip,
                "<Keyboard>/k",
                ProductionFlipKeyboardBindingId);
        }

        [Test]
        public void KeyboardBindingSettingsService_UsesCanonicalActionPaths()
        {
            Assert.That(GameplayInputActionPaths.PlayerMove, Is.EqualTo("Player/Move"));
            Assert.That(GameplayInputActionPaths.PlayerPush, Is.EqualTo("Player/Push"));
            Assert.That(GameplayInputActionPaths.PlayerFlip, Is.EqualTo("Player/Flip"));
            Assert.That(GameplayInputActionPaths.UiNavigate, Is.EqualTo("UI/Navigate"));
        }

        [TestCase(KeyboardMovementScheme.Wasd)]
        [TestCase(KeyboardMovementScheme.ArrowKeys)]
        public void ProductionInputActions_RebindSaveRoundTrip_PreservesBothMovementSchemes(KeyboardMovementScheme scheme)
        {
            var production = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProductionActionsPath);
            Assert.That(production, Is.Not.Null);
            var actions = CloneActions(production);
            var store = new FakeKeyboardBindingStore { MovementScheme = scheme };
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                using (var service = new KeyboardBindingSettingsService(actions, store))
                {
                    Rebind(service, keyboard, KeyboardBindableAction.Push, Key.R);
                    Rebind(service, keyboard, KeyboardBindableAction.Flip, Key.T);
                }

                TestContext.WriteLine("PRODUCTION_SAVED_BINDINGS " + scheme + " " + store.BindingOverridesJson);
                var restored = CloneActions(production);
                using var restoredService = new KeyboardBindingSettingsService(restored, store);
                Assert.That(restoredService.Read().MovementScheme, Is.EqualTo(scheme));
                Assert.That(HasEffectivePath(restored, GameplayInputActionPaths.PlayerPush, "<Keyboard>/r"), Is.True);
                Assert.That(HasEffectivePath(restored, GameplayInputActionPaths.PlayerFlip, "<Keyboard>/t"), Is.True);
                Assert.That(store.ClearCount, Is.Zero);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static void Rebind(KeyboardBindingSettingsService service, Keyboard keyboard, KeyboardBindableAction action, Key key)
        {
            KeyboardRebindResult? completed = null;
            Assert.That(service.StartRebind(action, result => completed = result).Started, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            // Interactive rebinding waits 50ms for competing controls; exercise that production path.
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!completed.HasValue && timeout.ElapsedMilliseconds < 2000)
            {
                System.Threading.Thread.Sleep(10);
                InputSystem.Update();
            }

            Assert.That(completed.HasValue, Is.True, "Interactive rebind did not complete.");
            Assert.That(completed.Value.Status, Is.EqualTo(KeyboardBindingValidationStatus.Success));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        [TestCase(KeyboardMovementScheme.Wasd)]
        [TestCase(KeyboardMovementScheme.ArrowKeys)]
        public void ProductionInputActions_LoadsPreRemovalSavedBindings_WithoutResettingSettings(KeyboardMovementScheme scheme)
        {
            var actions = CloneActions(AssetDatabase.LoadAssetAtPath<InputActionAsset>(ProductionActionsPath));
            var store = new FakeKeyboardBindingStore
            {
                MovementScheme = scheme,
                BindingOverridesJson = PreRemovalProductionOverridesJson,
            };
            using var service = new KeyboardBindingSettingsService(actions, store);

            Assert.That(actions.FindActionMap("Player").actions.Select(action => action.name),
                Is.EqualTo(new[] { "Move", "Push", "Flip" }));
            Assert.That(service.Read().MovementScheme, Is.EqualTo(scheme));
            Assert.That(service.Read().PushDisplayName, Is.EqualTo("R"));
            Assert.That(service.Read().FlipDisplayName, Is.EqualTo("T"));
            Assert.That(HasEffectivePath(actions, GameplayInputActionPaths.PlayerPush, "<Keyboard>/r"), Is.True);
            Assert.That(HasEffectivePath(actions, GameplayInputActionPaths.PlayerFlip, "<Keyboard>/t"), Is.True);
            foreach (var path in new[] { GameplayInputActionPaths.PlayerMove, GameplayInputActionPaths.UiNavigate })
            {
                Assert.That(IsEffective(actions, path, "<Keyboard>/w"), Is.EqualTo(scheme == KeyboardMovementScheme.Wasd));
                Assert.That(IsEffective(actions, path, "<Keyboard>/upArrow"), Is.EqualTo(scheme == KeyboardMovementScheme.ArrowKeys));
            }
            Assert.That(store.ClearCount, Is.Zero);
            Assert.That(store.SaveCount, Is.Zero);
            Assert.That(store.BindingOverridesJson, Is.EqualTo(PreRemovalProductionOverridesJson));

            var defaults = service.ResetToDefaults();
            Assert.That(defaults.MovementScheme, Is.EqualTo(KeyboardMovementScheme.Wasd));
            Assert.That(defaults.PushDisplayName, Is.EqualTo("J"));
            Assert.That(defaults.FlipDisplayName, Is.EqualTo("K"));
            Assert.That(store.BindingOverridesJson, Is.Null);
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
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("J"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("K"));
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
        public void KeyboardBindingSettingsService_ResetToDefaults_RestoresWasdPushJFlipK()
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
            Assert.That(snapshot.PushDisplayName, Is.EqualTo("J"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("K"));
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

            Assert.That(snapshot.PushDisplayName, Is.EqualTo("J"));
            Assert.That(snapshot.FlipDisplayName, Is.EqualTo("K"));
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

        private static void AssertProductionKeyboardBinding(
            InputActionAsset actions,
            string actionPath,
            string expectedPath,
            string expectedBindingId)
        {
            var action = actions.FindAction(actionPath, throwIfNotFound: true);
            var binding = action.bindings.Single(candidate =>
                !candidate.isComposite &&
                !candidate.isPartOfComposite &&
                candidate.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase));

            Assert.That(binding.path, Is.EqualTo(expectedPath));
            Assert.That(binding.id.ToString(), Is.EqualTo(expectedBindingId));
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
                    push.AddBinding("<Keyboard>/j")
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
                    flip.AddBinding("<Keyboard>/k")
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

            public int ClearCount { get; private set; }

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
                ClearCount++;
                BindingOverridesJson = null;
            }

            public void Save()
            {
                SaveCount++;
            }
        }
    }
}
